using UnityEditor;

public static class ReimportCircleSprite
{
    public static void Execute()
    {
        const string assetPath = "Assets/Sprites/UI/Circle.png";

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            UnityEngine.Debug.LogError("Importer not found: " + assetPath);
            return;
        }

        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode          = UnityEngine.FilterMode.Bilinear;
        importer.mipmapEnabled       = false;
        importer.alphaIsTransparency = true;

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        UnityEngine.Debug.Log("Reimport done: " + assetPath);
    }
}
