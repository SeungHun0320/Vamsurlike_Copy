using UnityEditor;
using UnityEngine;

public static class CreateWhiteSquare
{
    private const string SavePath = "Assets/Resources/Sprites/UI/WhiteSquare.png";

    public static void Execute()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources/Sprites/UI");

        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var pixels = new Color32[64 * 64];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);

        System.IO.File.WriteAllBytes(SavePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SavePath);

        // Sprite로 임포트 설정
        var importer = (TextureImporter)AssetImporter.GetAtPath(SavePath);
        if (importer != null)
        {
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePivot         = new Vector2(0.5f, 0.5f);
            importer.mipmapEnabled       = false;
            importer.filterMode          = FilterMode.Point;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        Debug.Log($"[CreateWhiteSquare] 생성 완료: {SavePath}");
    }
}
