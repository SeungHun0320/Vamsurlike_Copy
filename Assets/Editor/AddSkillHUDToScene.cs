using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vamsurlike.UI;

public static class AddSkillHUDToScene
{
    public static void Execute()
    {
        var hudCanvas = GameObject.Find("UI/HUDCanvas");
        if (hudCanvas == null)
        {
            Debug.LogError("UI/HUDCanvas not found in scene.");
            return;
        }

        // 기존에 있으면 스킵
        var existing = hudCanvas.transform.Find("SkillHUD");
        if (existing != null)
        {
            Debug.Log("SkillHUD already exists.");
            return;
        }

        var cellPrefab = AssetDatabase.LoadAssetAtPath<SkillHUDCellUI>("Assets/Prefabs/UI/SkillHUDCell.prefab");
        if (cellPrefab == null)
        {
            Debug.LogError("SkillHUDCell.prefab not found.");
            return;
        }

        var go = new GameObject("SkillHUD");
        go.transform.SetParent(hudCanvas.transform, false);

        var ui = go.AddComponent<SkillHUDUI>();

        var so = new SerializedObject(ui);
        so.FindProperty("cellPrefab").objectReferenceValue = cellPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("SkillHUD created and saved.");
    }
}
