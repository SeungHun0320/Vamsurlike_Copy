using UnityEditor;
using UnityEngine;

public static class FixCircleSpriteImport
{
    public static void Execute()
    {
        const string path = "Assets/Resources/Sprites/UI/Circle.png";

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("Not found: " + path);
            return;
        }

        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode          = FilterMode.Bilinear;
        importer.mipmapEnabled       = false;
        importer.alphaIsTransparency = true;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Debug.Log(sprite != null ? "Sprite OK: " + path : "Sprite STILL NULL after reimport");
    }
}
