using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI;

public static class CreateMainMenuButton
{
    public static void Execute()
    {
        var root = GameObject.Find("UI/StageResultCanvas/StageResultRoot");
        if (root == null)
        {
            // 경로 검색 대체
            var banner = Object.FindObjectOfType<StageClearBannerUI>(true);
            if (banner != null) root = banner.gameObject;
        }
        if (root == null) { Debug.LogError("[CreateMainMenuButton] StageResultRoot not found"); return; }

        // 기존 mainMenuButton 할당 제거 (ContinueButton과 충돌 방지)
        var stageResultUI = root.GetComponent<StageResultUI>();
        if (stageResultUI != null)
        {
            var so = new SerializedObject(stageResultUI);
            so.FindProperty("mainMenuButton").objectReferenceValue = null;
            so.ApplyModifiedProperties();
        }

        // 기존 MainMenuButton 제거
        var existing = root.transform.Find("MainMenuButton");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // ContinueButton과 같은 크기/스타일로 생성
        var btnGO = new GameObject("MainMenuButton", typeof(RectTransform));
        btnGO.transform.SetParent(root.transform, false);

        var rt = (RectTransform)btnGO.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -80f); // ContinueButton(0,0) 아래
        rt.sizeDelta = new Vector2(260f, 60f);

        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0.18f, 0.42f, 0.78f, 1f);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;

        // 라벨
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(btnGO.transform, false);
        var lrt = (RectTransform)labelGO.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "로비로 돌아가기";
        tmp.fontSize = 26;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        // StageResultUI에 연결
        if (stageResultUI != null)
        {
            var so2 = new SerializedObject(stageResultUI);
            so2.FindProperty("mainMenuButton").objectReferenceValue = btn;
            so2.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(root);
        Debug.Log("[CreateMainMenuButton] MainMenuButton 생성 완료 (0, -80)");
    }
}
