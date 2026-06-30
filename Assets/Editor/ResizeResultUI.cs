using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI;

public static class ResizeResultUI
{
    public static void Execute()
    {
        ResizeResultRow();
        ResizeSkillRow();
        AssetDatabase.SaveAssets();
        Debug.Log("[ResizeResultUI] 완료");
    }

    private static void ResizeResultRow()
    {
        const string path = "Assets/Prefabs/UI/PlayerResultRow.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) { Debug.LogError($"[ResizeResultUI] 프리팹 없음: {path}"); return; }

        // TMP 텍스트 크기
        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            switch (tmp.gameObject.name)
            {
                case "PlayerNameText": tmp.fontSize = 26f; break;
                case "StatsText":     tmp.fontSize = 22f; break;
                case "ExpandIcon":    tmp.fontSize = 20f; break;
            }
        }

        // 행 높이
        var rowUI = root.GetComponent<PlayerResultRowUI>();
        if (rowUI != null)
        {
            var so = new SerializedObject(rowUI);
            so.FindProperty("collapsedHeight").floatValue = 64f;
            so.FindProperty("skillRowHeight").floatValue  = 32f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Header 높이도 맞춤
        var headerRt = root.transform.Find("Header")?.GetComponent<RectTransform>();
        if (headerRt != null)
            headerRt.sizeDelta = new Vector2(headerRt.sizeDelta.x, 64f);

        var rootRt = root.GetComponent<RectTransform>();
        if (rootRt != null)
            rootRt.sizeDelta = new Vector2(rootRt.sizeDelta.x, 64f);

        // PlayerNameText 최소 너비
        var nameGO = root.transform.Find("Header/PlayerNameText");
        if (nameGO != null)
        {
            var le = nameGO.GetComponent<LayoutElement>();
            if (le != null) le.preferredWidth = 140f;
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void ResizeSkillRow()
    {
        const string path = "Assets/Prefabs/UI/PlayerSkillRow.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) { Debug.LogWarning($"[ResizeResultUI] 프리팹 없음: {path}"); return; }

        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            tmp.fontSize = Mathf.Max(tmp.fontSize * 1.4f, 20f);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
