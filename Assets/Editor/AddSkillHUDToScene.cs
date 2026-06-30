using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vamsurlike.UI;

public static class AddSkillHUDToScene
{
    public static void Execute()
    {
        var hudCanvas = GameObject.Find("UI/HUDCanvas");
        if (hudCanvas == null) { Debug.LogError("UI/HUDCanvas not found."); return; }

        // 기존 오브젝트 제거
        var existing = hudCanvas.transform.Find("SkillHUD");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
            Debug.Log("Removed existing SkillHUD.");
        }

        var cellPrefab = AssetDatabase.LoadAssetAtPath<SkillHUDCellUI>("Assets/Prefabs/UI/SkillHUDCell.prefab");
        if (cellPrefab == null) { Debug.LogError("SkillHUDCell.prefab not found."); return; }

        // RectTransform 포함 UI GameObject 생성
        var go = new GameObject("SkillHUD", typeof(RectTransform));
        go.transform.SetParent(hudCanvas.transform, false);

        // 캔버스 전체를 채우는 스트레치 설정
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;

        var ui = go.AddComponent<SkillHUDUI>();

        var so = new SerializedObject(ui);
        so.FindProperty("cellPrefab").objectReferenceValue = cellPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("SkillHUD created as UI element and saved.");
    }
}
