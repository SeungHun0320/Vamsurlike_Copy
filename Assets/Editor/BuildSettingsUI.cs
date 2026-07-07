using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI;

public static class BuildSettingsUI
{
    private static readonly Color PanelBg     = new(0.08f, 0.09f, 0.12f, 0.97f);
    private static readonly Color DialogBg    = new(0.12f, 0.13f, 0.18f, 1f);
    private static readonly Color LabelColor  = new(0.85f, 0.88f, 1f, 1f);
    private static readonly Color ButtonColor = new(0.2f, 0.5f, 0.9f, 1f);
    private static readonly Color SliderFill  = new(0.3f, 0.6f, 1f, 1f);

    private const float RowH   = 72f;
    private const float LabelW = 140f;
    private const float PadX   = 40f;
    private const float Gap    = 12f;

    public static void Execute()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[BuildSettingsUI] 'Canvas' not found in scene."); return; }

        // ── 기존 패널 제거 ──────────────────────────────────────────
        var existing = canvas.transform.Find("SettingsPanel");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
            Debug.Log("[BuildSettingsUI] 기존 SettingsPanel 제거.");
        }

        // ── SettingsPanel (SettingsUI 컴포넌트, 전체화면 반투명 오버레이) ──
        var panelGO = CreateGO("SettingsPanel", canvas.transform);
        SetStretch(panelGO);
        AddImage(panelGO, PanelBg);
        var settingsUI = panelGO.AddComponent<SettingsUI>();

        // ── Dialog (중앙 다이얼로그 박스) ────────────────────────────
        var dialog = CreateGO("Dialog", panelGO.transform);
        var dialogRect = dialog.GetComponent<RectTransform>();
        SetCenter(dialogRect, new Vector2(680f, 740f));
        AddImage(dialog, DialogBg);

        // y 값은 dialog 상단 앵커(0.5, 1)에서 아래로: 음수 = 안쪽
        float y = -30f;
        var title = CreateText("Title", dialog.transform, "설정", 42, LabelColor);
        SetAnchored(title.gameObject, new Vector2(0f, y), new Vector2(680f - PadX * 2, 50f));

        // ── 볼륨 슬라이더 3개 ────────────────────────────────────────
        y -= 80f;
        var masterSlider = CreateSliderRow("MasterRow", dialog.transform, "마스터", ref y);
        var bgmSlider    = CreateSliderRow("BgmRow",    dialog.transform, "BGM",    ref y);
        var sfxSlider    = CreateSliderRow("SfxRow",    dialog.transform, "효과음", ref y);

        // ── 해상도 드롭다운 + 전체화면 토글 ─────────────────────────
        y -= Gap;
        var resRow       = CreateGO("ResolutionRow", dialog.transform);
        var resDropdown  = CreateDropdownRow(resRow, dialog.transform, "해상도", ref y);

        y -= Gap;
        var fsRow        = CreateGO("FullscreenRow", dialog.transform);
        var fsToggle     = CreateToggleRow(fsRow, dialog.transform, "전체화면", ref y);

        // ── 프레임 캡 드롭다운 ────────────────────────────────────────
        y -= Gap;
        var fcRow        = CreateGO("FrameCapRow", dialog.transform);
        var fcDropdown   = CreateDropdownRow(fcRow, dialog.transform, "프레임 캡", ref y);

        // ── 닫기 버튼 ────────────────────────────────────────────────
        y -= Gap + 10f;
        var closeBtn = CreateButton("CloseButton", dialog.transform, "닫기", ref y, 200f, 56f);

        // ── SettingsButton (Canvas 우상단) ────────────────────────────
        var existing2 = canvas.transform.Find("SettingsButton");
        if (existing2 != null) Object.DestroyImmediate(existing2.gameObject);

        var settingsBtnGO = CreateGO("SettingsButton", canvas.transform);
        var sbRect = settingsBtnGO.GetComponent<RectTransform>();
        sbRect.anchorMin = sbRect.anchorMax = sbRect.pivot = new Vector2(1f, 1f);
        sbRect.anchoredPosition = new Vector2(-20f, -20f);
        sbRect.sizeDelta = new Vector2(120f, 56f);
        AddImage(settingsBtnGO, ButtonColor);
        var sbBtn = settingsBtnGO.AddComponent<Button>();
        sbBtn.targetGraphic = settingsBtnGO.GetComponent<Image>();
        CreateTextChild("Text", settingsBtnGO.transform, "설정", 28, Color.white);
        sbBtn.onClick.AddListener(() => panelGO.SetActive(true));

        // ── 레퍼런스 연결 ────────────────────────────────────────────
        var so = new SerializedObject(settingsUI);
        so.FindProperty("masterSlider").objectReferenceValue      = masterSlider;
        so.FindProperty("bgmSlider").objectReferenceValue         = bgmSlider;
        so.FindProperty("sfxSlider").objectReferenceValue         = sfxSlider;
        so.FindProperty("resolutionDropdown").objectReferenceValue = resDropdown;
        so.FindProperty("fullscreenToggle").objectReferenceValue  = fsToggle;
        so.FindProperty("frameCapDropdown").objectReferenceValue  = fcDropdown;
        so.FindProperty("closeButton").objectReferenceValue       = closeBtn;
        so.ApplyModifiedProperties();

        // 닫기 버튼 → SetActive(false)
        closeBtn.onClick.AddListener(() => panelGO.SetActive(false));

        // 처음엔 숨김
        panelGO.SetActive(false);

        EditorUtility.SetDirty(canvas);
        Debug.Log("[BuildSettingsUI] 완료 — SettingsPanel + SettingsButton 생성.");
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    private static GameObject CreateGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void SetStretch(GameObject go)
    {
        var r = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private static void SetCenter(RectTransform r, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = size;
    }

    private static void SetAnchored(GameObject go, Vector2 pos, Vector2 size)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color)
    {
        var go = CreateGO(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static void CreateTextChild(string name, Transform parent, string text, float size, Color color)
    {
        var go = CreateGO(name, parent);
        SetStretch(go);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private static Slider CreateSliderRow(string rowName, Transform parent, string label, ref float y)
    {
        float rowH = RowH;
        var row = CreateGO(rowName, parent);
        var rr = row.GetComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.anchoredPosition = new Vector2(0f, y);
        rr.sizeDelta = new Vector2(680f - PadX * 2, rowH);
        y -= rowH + Gap;

        // Label
        var lbl = CreateGO("Label", row.transform);
        var lr = lbl.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(0f, 1f);
        lr.pivot = new Vector2(0f, 0.5f);
        lr.anchoredPosition = Vector2.zero;
        lr.sizeDelta = new Vector2(LabelW, 0f);
        var lt = lbl.AddComponent<TextMeshProUGUI>();
        lt.text = label; lt.fontSize = 28; lt.color = LabelColor;
        lt.alignment = TextAlignmentOptions.MidlineLeft;

        // Slider
        var sliderGO = CreateGO("Slider", row.transform);
        var sliderRect = sliderGO.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f); sliderRect.anchorMax = Vector2.one;
        sliderRect.offsetMin = new Vector2(LabelW + 10f, 8f);
        sliderRect.offsetMax = new Vector2(0f, -8f);

        // Background
        var bg = CreateGO("Background", sliderGO.transform);
        SetStretch(bg);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.3f, 1f);

        // Fill Area
        var fillArea = CreateGO("Fill Area", sliderGO.transform);
        var far = fillArea.GetComponent<RectTransform>();
        far.anchorMin = Vector2.zero; far.anchorMax = Vector2.one;
        far.offsetMin = new Vector2(0f, 0f); far.offsetMax = new Vector2(-10f, 0f);

        var fill = CreateGO("Fill", fillArea.transform);
        SetStretch(fill);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = SliderFill;

        // Handle
        var handleArea = CreateGO("Handle Slide Area", sliderGO.transform);
        SetStretch(handleArea);
        var handle = CreateGO("Handle", handleArea.transform);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(20f, 20f);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        var slider = sliderGO.AddComponent<Slider>();
        slider.fillRect  = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handleImg;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
        slider.direction = Slider.Direction.LeftToRight;

        return slider;
    }

    private static TMP_Dropdown CreateDropdownRow(GameObject rowGO, Transform parent, string label, ref float y)
    {
        float rowH = RowH;
        var rr = rowGO.GetComponent<RectTransform>();
        rowGO.transform.SetParent(parent, false);
        rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.anchoredPosition = new Vector2(0f, y);
        rr.sizeDelta = new Vector2(680f - PadX * 2, rowH);
        y -= rowH + Gap;

        var lbl = CreateGO("Label", rowGO.transform);
        var lr = lbl.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(0f, 1f);
        lr.pivot = new Vector2(0f, 0.5f);
        lr.anchoredPosition = Vector2.zero;
        lr.sizeDelta = new Vector2(LabelW, 0f);
        var lt = lbl.AddComponent<TextMeshProUGUI>();
        lt.text = label; lt.fontSize = 28; lt.color = LabelColor;
        lt.alignment = TextAlignmentOptions.MidlineLeft;

        var ddGO = CreateGO("Dropdown", rowGO.transform);
        var ddr = ddGO.GetComponent<RectTransform>();
        ddr.anchorMin = new Vector2(0f, 0f); ddr.anchorMax = Vector2.one;
        ddr.offsetMin = new Vector2(LabelW + 10f, 4f);
        ddr.offsetMax = new Vector2(0f, -4f);
        AddImage(ddGO, new Color(0.18f, 0.2f, 0.28f, 1f));

        var labelGO = CreateGO("Label", ddGO.transform);
        SetStretch(labelGO);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.fontSize = 26; labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        labelTMP.margin = new Vector4(10, 0, 10, 0);

        var dd = ddGO.AddComponent<TMP_Dropdown>();
        dd.captionText = labelTMP;
        dd.options = new List<TMP_Dropdown.OptionData> { new("--") };

        return dd;
    }

    private static Toggle CreateToggleRow(GameObject rowGO, Transform parent, string label, ref float y)
    {
        float rowH = RowH;
        var rr = rowGO.GetComponent<RectTransform>();
        rowGO.transform.SetParent(parent, false);
        rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.anchoredPosition = new Vector2(0f, y);
        rr.sizeDelta = new Vector2(680f - PadX * 2, rowH);
        y -= rowH + Gap;

        var lbl = CreateGO("Label", rowGO.transform);
        var lr = lbl.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(0f, 1f);
        lr.pivot = new Vector2(0f, 0.5f);
        lr.anchoredPosition = Vector2.zero;
        lr.sizeDelta = new Vector2(LabelW, 0f);
        var lt = lbl.AddComponent<TextMeshProUGUI>();
        lt.text = label; lt.fontSize = 28; lt.color = LabelColor;
        lt.alignment = TextAlignmentOptions.MidlineLeft;

        var toggleGO = CreateGO("Toggle", rowGO.transform);
        var tr = toggleGO.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0.5f); tr.anchorMax = new Vector2(0f, 0.5f);
        tr.pivot = new Vector2(0f, 0.5f);
        tr.anchoredPosition = new Vector2(LabelW + 10f, 0f);
        tr.sizeDelta = new Vector2(48f, 48f);

        var bgImg = AddImage(toggleGO, new Color(0.2f, 0.2f, 0.3f, 1f));

        var checkGO = CreateGO("Checkmark", toggleGO.transform);
        SetStretch(checkGO);
        var checkImg = checkGO.AddComponent<Image>();
        checkImg.color = SliderFill;

        var toggle = toggleGO.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;
        toggle.isOn = true;

        return toggle;
    }

    private static Button CreateButton(string name, Transform parent, string text, ref float y, float w, float h)
    {
        var go = CreateGO(name, parent);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, y);
        r.sizeDelta = new Vector2(w, h);
        y -= h + Gap;

        var img = AddImage(go, ButtonColor);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        CreateTextChild("Text", go.transform, text, 30, Color.white);
        return btn;
    }
}
