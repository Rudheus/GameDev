using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Ubah scene jadi atmosfer malam (jalankan: Tools → Setup Night Atmosphere):
// 1. Directional light → cahaya bulan (redup, kebiruan, bayangan lembut).
// 2. Fog gelap + ambient biru tua (jarak pandang terbatas, nuansa tegang).
// 3. Skybox malam prosedural (dibuat ke Assets/Material/NightSky.mat).
// 4. Post-processing di SampleSceneProfile: vignette, color grading dingin, bloom
//    (biar lampu spot yang kamu taruh nanti menyala/glow).
// Aman dijalankan ulang — nilai di-overwrite, nggak dobel.
public static class SetupNightAtmosphere
{
    const string SkyboxPath = "Assets/Material/NightSky.mat";
    const string ProfilePath = "Assets/Settings/SampleSceneProfile.asset";

    [MenuItem("Tools/Setup Night Atmosphere")]
    public static void Run()
    {
        SetupMoonlight();
        SetupFogAndAmbient();
        SetupSkybox();
        SetupPostProcessing();

        EditorSceneManager.MarkAllScenesDirty(); // biar perubahan scene ikut ke-save (Ctrl+S)
        Debug.Log("Setup Night Atmosphere selesai — cek Game view (fog di Scene view perlu toggle efek dinyalakan). Jangan lupa Ctrl+S.");
    }

    static void SetupMoonlight()
    {
        var moon = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
            .FirstOrDefault(l => l.type == LightType.Directional);
        if (moon == null)
        {
            Debug.LogWarning("SetupNightAtmosphere: Directional Light tidak ketemu di scene — lewati moonlight.");
            return;
        }

        moon.color = new Color(0.58f, 0.67f, 0.88f);  // biru pucat cahaya bulan
        moon.intensity = 0.35f;                        // redup — malam
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.9f;
        moon.transform.rotation = Quaternion.Euler(55f, -30f, 0f); // sudut tinggi ala bulan
        EditorUtility.SetDirty(moon);
    }

    static void SetupFogAndAmbient()
    {
        // Fog exponential: makin jauh makin gelap — nyembunyiin ujung level, bikin tegang.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.035f, 0.05f, 0.09f); // biru-hitam malam
        RenderSettings.fogDensity = 0.018f;

        // Ambient flat gelap kebiruan — area tanpa lampu tetap kebaca siluetnya.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.12f, 0.19f);
    }

    static void SetupSkybox()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(mat, SkyboxPath);
        }

        mat.SetFloat("_SunSize", 0.025f);              // "bulan" kecil
        mat.SetFloat("_AtmosphereThickness", 0.32f);   // atmosfer tipis = langit gelap
        mat.SetColor("_SkyTint", new Color(0.18f, 0.22f, 0.35f));
        mat.SetColor("_GroundColor", new Color(0.03f, 0.04f, 0.06f));
        mat.SetFloat("_Exposure", 0.28f);              // redupkan keseluruhan langit
        EditorUtility.SetDirty(mat);

        RenderSettings.skybox = mat;
    }

    static void SetupPostProcessing()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"SetupNightAtmosphere: {ProfilePath} tidak ketemu — lewati post-processing.");
            return;
        }

        // Vignette: pinggiran layar menggelap — fokus + rasa terkepung.
        if (!profile.TryGet(out Vignette vignette)) vignette = profile.Add<Vignette>(true);
        vignette.active = true;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.32f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.42f;

        // Color grading dingin: saturasi turun, kontras naik dikit.
        if (!profile.TryGet(out ColorAdjustments color)) color = profile.Add<ColorAdjustments>(true);
        color.active = true;
        color.saturation.overrideState = true;
        color.saturation.value = -14f;
        color.contrast.overrideState = true;
        color.contrast.value = 8f;
        color.colorFilter.overrideState = true;
        color.colorFilter.value = new Color(0.88f, 0.92f, 1f); // tint biru tipis

        // Bloom: lampu spot yang nanti dipasang bakal glow — kota malam hidup.
        if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.45f;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }
}
