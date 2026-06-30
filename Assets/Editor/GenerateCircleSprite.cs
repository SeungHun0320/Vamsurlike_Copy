using UnityEngine;
using UnityEditor;
using System.IO;

public static class GenerateCircleSprite
{
    public static void Execute()
    {
        const string assetPath = "Assets/Sprites/UI/Circle.png";
        string fullPath = Path.Combine(Application.dataPath, "../" + assetPath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

        const int res = 128;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float center = res * 0.5f;
        var pixels = new Color32[res * res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx   = x - center + 0.5f;
                float dy   = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                byte  a    = (byte)(Mathf.Clamp01(center - dist) * 255f);
                pixels[y * res + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.Refresh();

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (importer != null)
        {
            importer.textureType         = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode          = FilterMode.Bilinear;
            importer.mipmapEnabled       = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        Debug.Log("Circle sprite created: " + assetPath);
    }
}
