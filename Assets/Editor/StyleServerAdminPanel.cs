using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class StyleServerAdminPanel
{
    public static void Execute()
    {
        var root = GameObject.Find("Canvas");
        if (root == null) { Debug.LogError("Canvas not found"); return; }

        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Resources/Fonts/MalgunGothic SDF.asset");

        // 패널 배경 투명하게
        var panel = root.transform.Find("ServerAdminPanel");
        if (panel != null)
        {
            var img = panel.GetComponent<Image>();
            if (img != null) { img.color = new Color(0, 0, 0, 0); EditorUtility.SetDirty(img); }
        }

        // 게임 시작 버튼 파란색
        var btn = root.transform.Find("ServerAdminPanel/StartGameButton");
        if (btn != null)
        {
            var img = btn.GetComponent<Image>();
            if (img != null) { img.color = new Color(0.2f, 0.5f, 0.9f, 1f); EditorUtility.SetDirty(img); }

            var tmp = btn.Find("Text")?.GetComponent<TextMeshProUGUI>()
                   ?? btn.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                // 레거시 Text → TMP 교체
                var legacyText = btn.Find("Text")?.GetComponent<Text>();
                if (legacyText != null)
                {
                    Object.DestroyImmediate(legacyText);
                    tmp = btn.Find("Text").gameObject.AddComponent<TextMeshProUGUI>();
                    tmp.text = "게임 시작";
                }
            }
            if (tmp != null)
            {
                if (fontAsset != null) tmp.font = fontAsset;
                tmp.color = Color.white;
                tmp.fontSize = 24;
                tmp.alignment = TextAlignmentOptions.Center;
                EditorUtility.SetDirty(tmp);
            }
        }

        // 플레이어 수 텍스트 폰트
        var countText = root.transform.Find("ServerAdminPanel/PlayerCountText")?.GetComponent<TextMeshProUGUI>();
        if (countText != null)
        {
            if (fontAsset != null) countText.font = fontAsset;
            countText.color = Color.white;
            countText.fontSize = 24;
            countText.alignment = TextAlignmentOptions.Center;
            EditorUtility.SetDirty(countText);
        }
    }
}
