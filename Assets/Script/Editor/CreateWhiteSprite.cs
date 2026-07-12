using System.IO;
using UnityEditor;
using UnityEngine;

// Bikin sprite kotak putih polos (Assets/UI/WhiteSquare.png) buat elemen UI
// yang butuh sudut siku (stamina bar, dll.) — sprite bawaan Unity semuanya
// bersudut bulat. Jalankan sekali: Tools → Create White Square Sprite.
public static class CreateWhiteSprite
{
    const string Folder = "Assets/UI";
    const string Path = Folder + "/WhiteSquare.png";

    [MenuItem("Tools/Create White Square Sprite")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "UI");

        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var pixels = new Color32[64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();

        File.WriteAllBytes(Path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(Path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();

        Debug.Log($"Sprite kotak putih dibuat: {Path} — drag ke Source Image elemen UI yang butuh sudut siku.");
    }
}
