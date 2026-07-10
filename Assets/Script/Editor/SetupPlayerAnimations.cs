using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Otomatisasi setup animasi player (jalankan dari menu: Tools → Setup Player Animations):
// 1. Set semua FBX di Assets/Animation + model karakter ke rig Humanoid.
// 2. Nyalakan Loop Time untuk clip yang memang looping (Idle/Walking/Running).
// 3. Bangun PlayerAnimator.controller: blend tree Idle→Walk→Run (param Speed),
//    state InAir (IsGrounded false) dan Dash (IsDashing true).
// Aman dijalankan ulang — controller dibangun ulang dari nol tiap kali.
public static class SetupPlayerAnimations
{
    const string AnimFolder = "Assets/Animation";
    const string ControllerPath = AnimFolder + "/PlayerAnimator.controller";
    const string EnemyControllerPath = AnimFolder + "/EnemyAnimator.controller";
    const string CharacterFbx = "Assets/Prefab/prisoner/source/Prisoner@T-Pose.fbx";
    const string PoliceFbx = "Assets/Prefab/police/source/Happy Idle.fbx";

    // Clip gerak siklik — harus loop. Sisanya (Jump, Roll, Slide) sekali jalan.
    static readonly string[] LoopedClips = { "Idle", "Walking", "Running", "Sprint" };

    [MenuItem("Tools/Setup Player Animations")]
    public static void Run()
    {
        // --- 1 & 2: importer semua FBX animasi ---
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AnimFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            bool loop = LoopedClips.Contains(Path.GetFileNameWithoutExtension(path));
            ConfigureImporter(path, loop);
        }

        // Model karakter juga harus Humanoid supaya retarget animasi Mixamo jalan.
        ConfigureImporter(CharacterFbx, false);
        ConfigureImporter(PoliceFbx, false);

        // --- 3: bangun controller ---
        BuildController();
        BuildEnemyController();

        Debug.Log($"Setup Player Animations selesai — controller player di {ControllerPath}, " +
                  $"controller polisi di {EnemyControllerPath}.");
    }

    static void ConfigureImporter(string path, bool loop)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"SetupPlayerAnimations: {path} bukan model, dilewati.");
            return;
        }

        importer.animationType = ModelImporterAnimationType.Human; // Humanoid → retarget antar rig

        // Rename clip ke nama file (default Mixamo: "mixamo.com") + set loop.
        var clips = importer.defaultClipAnimations;
        if (clips.Length > 0)
        {
            string niceName = Path.GetFileNameWithoutExtension(path);

            // Gerakan turun (pinggul merendah) di clip slide tersimpan sebagai root motion Y.
            // Root motion kita matikan, jadi Y harus di-bake ke pose supaya badannya
            // benar-benar kelihatan turun. Clip lain jangan — jump/roll Y-nya diurus physics.
            bool bakeY = niceName == "Running Slide";

            foreach (var clip in clips)
            {
                clip.name = niceName;
                clip.loopTime = loop;

                // Slide: bake Y ke pose dengan acuan Feet (badan nempel tanah, anti-melayang).
                // Clip lain: biarkan default — acuan Original (CoM/Feet bikin badan tenggelam).
                clip.lockRootHeightY = bakeY;         // Bake Into Pose (Position Y)
                clip.keepOriginalPositionY = !bakeY;  // non-slide: Based Upon Original
                clip.heightFromFeet = bakeY;          // slide: Based Upon Feet
            }
            importer.clipAnimations = clips;
        }

        importer.SaveAndReimport();
    }

    static AnimationClip LoadClip(string name)
    {
        string path = $"{AnimFolder}/{name}.fbx";
        var clip = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview"));
        if (clip == null) Debug.LogError($"SetupPlayerAnimations: clip tidak ketemu di {path}");
        return clip;
    }

    static void BuildController()
    {
        AssetDatabase.DeleteAsset(ControllerPath); // rebuild bersih, aman diulang
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("IsDashing", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("IsSliding", AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;

        // Locomotion: blend 1D berdasar laju horizontal aktual.
        // Threshold cocok dengan PlayerController (moveSpeed 5, sprintSpeed 8), dan
        // timeScale tiap clip dipercepat supaya laju kaki ≈ laju badan (anti foot-skating):
        // laju natural Mixamo kira-kira: Walking 1.7, Running 4.2, Sprint 6.3 m/s.
        var locomotion = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(LoadClip("Idle"), 0f);
        tree.AddChild(LoadClip("Walking"), 2.5f);
        tree.AddChild(LoadClip("Running"), 5f);

        // Sprint pakai clip sendiri kalau ada (Assets/Animation/Sprint.fbx);
        // fallback: Running dipercepat (agak komikal tapi nggak meluncur).
        bool hasSprint = File.Exists($"{AnimFolder}/Sprint.fbx");
        tree.AddChild(LoadClip(hasSprint ? "Sprint" : "Running"), 8f);

        var children = tree.children;
        children[1].timeScale = 1.4f;                      // Walking → 2.5 m/s
        children[2].timeScale = 1.2f;                      // Running → 5 m/s
        children[3].timeScale = hasSprint ? 1.25f : 1.9f;  // Sprint → 8 m/s
        tree.children = children;

        sm.defaultState = locomotion;

        // InAir: dipakai untuk lompat DAN jatuh dari pinggiran (dua-duanya IsGrounded false).
        var inAir = sm.AddState("InAir");
        inAir.motion = LoadClip("Jumping Up");

        var dash = sm.AddState("Dash");
        dash.motion = LoadClip("Run To Rolling");

        // Locomotion ⇄ InAir mengikuti IsGrounded.
        var toAir = locomotion.AddTransition(inAir);
        toAir.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
        toAir.hasExitTime = false;
        toAir.duration = 0.1f;

        var toGround = inAir.AddTransition(locomotion);
        toGround.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
        toGround.hasExitTime = false;
        toGround.duration = 0.15f;

        // Dash bisa dari state mana pun; keluar begitu IsDashing selesai.
        var anyToDash = sm.AddAnyStateTransition(dash);
        anyToDash.AddCondition(AnimatorConditionMode.If, 0f, "IsDashing");
        anyToDash.hasExitTime = false;
        anyToDash.duration = 0.05f;
        anyToDash.canTransitionToSelf = false;

        var dashOut = dash.AddTransition(locomotion);
        dashOut.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDashing");
        dashOut.hasExitTime = false;
        dashOut.duration = 0.15f;

        // Slide: sama polanya dengan Dash.
        var slide = sm.AddState("Slide");
        slide.motion = LoadClip("Running Slide");

        var anyToSlide = sm.AddAnyStateTransition(slide);
        anyToSlide.AddCondition(AnimatorConditionMode.If, 0f, "IsSliding");
        anyToSlide.hasExitTime = false;
        anyToSlide.duration = 0.05f;
        anyToSlide.canTransitionToSelf = false;

        var slideOut = slide.AddTransition(locomotion);
        slideOut.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSliding");
        slideOut.hasExitTime = false;
        slideOut.duration = 0.15f;

        AssetDatabase.SaveAssets();
    }

    // Controller polisi: cukup blend Idle/Walk/Run — clip yang SAMA dengan player,
    // di-retarget Humanoid ke model polisi. Threshold cocok dengan EnemyChaser
    // (patrolSpeed 2, moveSpeed 3.5); timeScale disetel anti foot-skating.
    static void BuildEnemyController()
    {
        AssetDatabase.DeleteAsset(EnemyControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(EnemyControllerPath);

        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);

        var sm = ctrl.layers[0].stateMachine;
        var locomotion = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(LoadClip("Idle"), 0f);
        tree.AddChild(LoadClip("Walking"), 2f);
        tree.AddChild(LoadClip("Running"), 3.5f);

        var children = tree.children;
        children[1].timeScale = 1.2f;   // Walking → patroli 2 m/s
        children[2].timeScale = 0.85f;  // Running → kejar 3.5 m/s
        tree.children = children;

        sm.defaultState = locomotion;
        AssetDatabase.SaveAssets();
    }
}
