using UnityEditor;
using UnityEngine;
using Vamsurlike.UI;

public static class AssignCircleSpriteToPrefab
{
    public static void Execute()
    {
        const string spritePath = "Assets/Resources/Sprites/UI/Circle.png";
        const string prefabPath = "Assets/Prefabs/Player/NetworkedPlayer.prefab";

        // 임포트 설정 확인 및 강제 적용
        var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("Sprite not found: " + spritePath);
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

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            Debug.LogError("Sprite load failed after reimport: " + spritePath);
            return;
        }

        // 프리팹에 직접 할당
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogError("Prefab not found"); return; }

        var ui = prefab.GetComponent<WorldDownedTimerUI>();
        if (ui == null) { Debug.LogError("WorldDownedTimerUI not found on prefab"); return; }

        var so = new SerializedObject(ui);
        var prop = so.FindProperty("circleSprite");
        prop.objectReferenceValue = sprite;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SavePrefabAsset(prefab);
        Debug.Log("Assigned circle sprite to prefab successfully.");
    }
}
