using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI;

public static class CreateCoopSlotPrefab
{
    private const string SavePath = "Assets/Prefabs/UI/PlayerSlotUI.prefab";

    public static void Execute()
    {
        // Root
        var root = new GameObject("PlayerSlotUI");
        root.AddComponent<CanvasRenderer>();
        var rootImg  = root.AddComponent<Image>();
        rootImg.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);
        rootImg.raycastTarget = false;

        // HPBarBg
        var hpBgGO = new GameObject("HPBarBg");
        hpBgGO.transform.SetParent(root.transform, false);
        hpBgGO.AddComponent<CanvasRenderer>();
        var hpBgImg  = hpBgGO.AddComponent<Image>();
        hpBgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        hpBgImg.raycastTarget = false;
        var hpBgRt   = hpBgGO.GetComponent<RectTransform>();
        hpBgRt.anchorMin        = new Vector2(0f, 0f);
        hpBgRt.anchorMax        = new Vector2(1f, 0f);
        hpBgRt.pivot            = new Vector2(0.5f, 0f);
        hpBgRt.sizeDelta        = new Vector2(-20f, 14f);
        hpBgRt.anchoredPosition = new Vector2(0f, 8f);

        // HPFill
        var fillGO = new GameObject("HPFill");
        fillGO.transform.SetParent(hpBgGO.transform, false);
        fillGO.AddComponent<CanvasRenderer>();
        var fill    = fillGO.AddComponent<Image>();
        fill.color  = new Color(0.2f, 0.8f, 0.2f, 1f);
        fill.raycastTarget = false;
        fill.type       = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        if (fill.sprite == null)
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var fillRt = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;

        // NameText
        var nameGO   = new GameObject("NameText");
        nameGO.transform.SetParent(root.transform, false);
        var nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text      = "PlayerName";
        nameText.fontSize  = 28f;
        nameText.color     = Color.white;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.overflowMode  = TextOverflowModes.Ellipsis;
        nameText.raycastTarget = false;
        var nameRt = nameGO.GetComponent<RectTransform>();
        nameRt.anchorMin = Vector2.zero;
        nameRt.anchorMax = Vector2.one;
        nameRt.offsetMin = new Vector2(10f, 26f);
        nameRt.offsetMax = new Vector2(-10f, -8f);

        // DownedOverlay
        var downGO = new GameObject("DownedOverlay");
        downGO.transform.SetParent(root.transform, false);
        downGO.SetActive(false);
        downGO.AddComponent<CanvasRenderer>();
        var downImg = downGO.AddComponent<Image>();
        downImg.color = new Color(0.9f, 0.3f, 0.1f, 0.55f);
        downImg.raycastTarget = false;
        StretchFull(downGO);

        var downTxtGO = new GameObject("Label");
        downTxtGO.transform.SetParent(downGO.transform, false);
        var downTxt = downTxtGO.AddComponent<TextMeshProUGUI>();
        downTxt.text      = "다운됨 0s";
        downTxt.fontSize  = 22f;
        downTxt.color     = Color.white;
        downTxt.fontStyle = FontStyles.Bold;
        downTxt.alignment = TextAlignmentOptions.MidlineRight;
        downTxt.margin    = new Vector4(0f, 0f, 10f, 0f);
        downTxt.raycastTarget = false;
        StretchFull(downTxtGO);

        // DeadOverlay
        var deadGO = new GameObject("DeadOverlay");
        deadGO.transform.SetParent(root.transform, false);
        deadGO.SetActive(false);
        deadGO.AddComponent<CanvasRenderer>();
        var deadImg = deadGO.AddComponent<Image>();
        deadImg.color = new Color(0f, 0f, 0f, 0.72f);
        deadImg.raycastTarget = false;
        StretchFull(deadGO);

        var deadTxtGO = new GameObject("Label");
        deadTxtGO.transform.SetParent(deadGO.transform, false);
        var deadTxt = deadTxtGO.AddComponent<TextMeshProUGUI>();
        deadTxt.text      = "0s";
        deadTxt.fontSize  = 28f;
        deadTxt.color     = new Color(0.8f, 0.8f, 0.8f, 1f);
        deadTxt.fontStyle = FontStyles.Bold;
        deadTxt.alignment = TextAlignmentOptions.MidlineRight;
        deadTxt.margin    = new Vector4(0f, 0f, 10f, 0f);
        deadTxt.raycastTarget = false;
        StretchFull(deadTxtGO);

        // PlayerSlotUI 컴포넌트 — 레퍼런스 연결
        var slotUI = root.AddComponent<PlayerSlotUI>();
        slotUI.hpFill         = fill;
        slotUI.nameText        = nameText;
        slotUI.downedOverlay   = downGO;
        slotUI.downedTimerText = downTxt;
        slotUI.deadOverlay     = deadGO;
        slotUI.deadTimerText   = deadTxt;

        // 프리팹 저장
        System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);

        if (prefab == null)
        {
            Debug.LogError($"[CreateCoopSlotPrefab] 저장 실패: {SavePath}");
            return;
        }

        Debug.Log($"[CreateCoopSlotPrefab] 프리팹 생성 완료: {SavePath}");
        AssetDatabase.Refresh();
    }

    private static void StretchFull(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
