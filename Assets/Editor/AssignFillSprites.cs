using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AssignFillSprites
{
    private const string SpritePath = "Assets/Resources/Sprites/UI/WhiteSquare.png";

    public static void Execute()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
        {
            Debug.LogError($"[AssignFillSprites] 스프라이트 없음: {SpritePath}");
            return;
        }

        int count = 0;

        // 열린 씬 — type=Filled 이고 sprite 없는 Image 전부
        foreach (var img in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (img.type != Image.Type.Filled) continue;
            if (img.sprite != null) continue;
            img.sprite = sprite;
            EditorUtility.SetDirty(img);
            count++;
        }

        // 프리팹
        string[] prefabPaths =
        {
            "Assets/Prefabs/UI/PlayerSlotUI.prefab",
            "Assets/Prefabs/UI/SkillHUDCell.prefab",
        };

        foreach (string path in prefabPaths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            bool dirty = false;
            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                if (img.type != Image.Type.Filled) continue;
                if (img.sprite != null) continue;
                img.sprite = sprite;
                dirty = true;
                count++;
            }

            if (dirty)
                PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[AssignFillSprites] {count}개 Image에 WhiteSquare 할당 완료");
    }
}
