using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI;

// 일회성 에디터 빌드 스크립트. execute_script로 Execute()를 실행해
// UpgradeCard 프리팹 + PermanentUpgradeShop 패널 하이어라키 + LobbyShopButton을 생성한다.
// coplay-mcp의 set_property가 Color/오브젝트 참조 필드를 안정적으로 반영하지 못해
// FixNetworkErrorDialog.cs와 동일한 SerializedObject 기반 패턴으로 대체.
public static class BuildPermanentUpgradeShopUI
{
    // 전체 UI 배율 — 카드/패널/버튼/폰트 크기를 이 값 기준으로 일괄 확대한다.
    private const float Scale = 1.5f;

    private static readonly Color PanelColor        = new(0.043f, 0.055f, 0.075f, 0.97f);
    private static readonly Color CardColor         = new(0.10f, 0.12f, 0.15f, 0.96f);
    private static readonly Color CardIconBgColor   = new(0.16f, 0.17f, 0.20f, 1f);
    private static readonly Color SegmentEmptyColor = new(0.24f, 0.26f, 0.30f, 1f);
    private static readonly Color GoldColor         = new(1f, 0.82f, 0.30f, 1f);
    private static readonly Color DescriptionColor  = new(0.68f, 0.73f, 0.80f, 1f);
    private static readonly Color NextValueColor    = new(0.42f, 0.90f, 0.55f, 1f);
    private static readonly Color ButtonColor       = new(0.20f, 0.40f, 0.72f, 1f);

    private const int MaxSegments = 8;
    private const string CardPrefabPath = "Assets/Prefabs/UI/UpgradeCard.prefab";

    private static float S(float v) => v * Scale;
    private static int   S(int v) => Mathf.RoundToInt(v * Scale);

    public static void ClickConnect()
    {
        var canvas = GameObject.Find("Canvas");
        var btn = canvas != null ? canvas.transform.Find("ConnectPanel/JoinButton") : null;
        if (btn == null) { Debug.LogError("[Build] JoinButton not found"); return; }
        btn.GetComponent<Button>().onClick.Invoke();
    }

    public static void DirectOpenShop()
    {
        var canvas = GameObject.Find("Canvas");
        var shop = canvas != null ? canvas.transform.Find("PermanentUpgradeShop") : null;
        if (shop == null) { Debug.LogError("[Build] PermanentUpgradeShop not found"); return; }
        shop.GetComponent<PermanentUpgradeShopUI>().OpenPanel();
    }

    public static void TestPurchaseFlow()
    {
        var canvas = GameObject.Find("Canvas");
        var shop = canvas != null ? canvas.transform.Find("PermanentUpgradeShop") : null;
        if (shop == null) { Debug.LogError("[Build] PermanentUpgradeShop not found"); return; }
        var ui = shop.GetComponent<PermanentUpgradeShopUI>();
        ui.OpenPanel();

        // 골드 지급 후 두 번째 카드(방어) 클릭 -> 구매 버튼 클릭까지 시뮬레이션
        var vmField = typeof(PermanentUpgradeShopUI).GetField("viewModel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var vm = (Vamsurlike.UI.ViewModels.PermanentUpgradeShopViewModel)vmField.GetValue(ui);
        vm.DebugGrantGold(500);
        vm.SelectDetail(Vamsurlike.Upgrades.PermanentUpgradeType.Defense);
        // 서버 왕복이 필요해져서(구매가 비동기), 결과는 더 이상 여기서 동기적으로 알 수 없다 —
        // 요청만 보내고 실제 반영 여부는 게임을 띄운 채 확인해야 한다.
        vm.RequestPurchase(Vamsurlike.Upgrades.PermanentUpgradeType.Defense);
        Debug.Log("[Build] TestPurchaseFlow: 구매 요청 전송(결과는 서버 동기화 후 확인).");
    }

    public static void ClickShopButton()
    {
        var canvas = GameObject.Find("Canvas");
        var btn = canvas != null ? canvas.transform.Find("LobbyShopButton") : null;
        if (btn == null) { Debug.LogError("[Build] LobbyShopButton not found"); return; }
        btn.GetComponent<Button>().onClick.Invoke();
    }

    public static void PreviewShopButton()
    {
        var canvas = GameObject.Find("Canvas");
        var btn = canvas != null ? canvas.transform.Find("LobbyShopButton") : null;
        if (btn == null) { Debug.LogError("[Build] LobbyShopButton not found"); return; }
        btn.gameObject.SetActive(true);
    }

    public static void PreviewOn() => SetShopActive(true);
    public static void PreviewOff() => SetShopActive(false);

    private static void SetShopActive(bool active)
    {
        var canvas = GameObject.Find("Canvas");
        var shop = canvas != null ? canvas.transform.Find("PermanentUpgradeShop") : null;
        if (shop == null) { Debug.LogError("[Build] PermanentUpgradeShop not found"); return; }
        shop.gameObject.SetActive(active);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public static void Execute()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[Build] Canvas not found"); return; }

        // 이전 시행착오로 남은 오브젝트 정리
        var strayCard = canvas.transform.Find("UpgradeCard");
        if (strayCard != null) Object.DestroyImmediate(strayCard.gameObject);
        var strayShop = canvas.transform.Find("PermanentUpgradeShop");
        if (strayShop != null) Object.DestroyImmediate(strayShop.gameObject);
        var strayButton = canvas.transform.Find("LobbyShopButton");
        if (strayButton != null) Object.DestroyImmediate(strayButton.gameObject);

        GameObject cardInstance = BuildCard();
        GameObject cardPrefabAsset = PrefabUtility.SaveAsPrefabAsset(cardInstance, CardPrefabPath);
        Object.DestroyImmediate(cardInstance);

        PermanentUpgradeShopUI shopUI = BuildShopPanel(canvas.transform, cardPrefabAsset);
        BuildLobbyShopButton(canvas.transform, shopUI);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)shopUI.transform);
        Canvas.ForceUpdateCanvases();

        EditorUtility.SetDirty(canvas);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[Build] PermanentUpgradeShop UI 생성 완료 (Scale=" + Scale + ")");
    }

    // ─── Card ───────────────────────────────────────────────────────

    private static GameObject BuildCard()
    {
        GameObject root = new GameObject("UpgradeCard", typeof(RectTransform));
        var rootRt = (RectTransform)root.transform;
        rootRt.sizeDelta = new Vector2(S(128f), S(148f));

        Image bg = root.AddComponent<Image>();
        bg.color = CardColor;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = bg;

        VerticalLayoutGroup vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(S(8), S(8), S(10), S(8));
        vlg.spacing = S(4f);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        (Image iconBg, TextMeshProUGUI iconGlyph) = CreateIconCircle(root.transform, S(56f), S(15));

        TextMeshProUGUI title = CreateText(root.transform, "Name", S(11), FontStyles.Bold, TextAlignmentOptions.Center);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;
        SetPreferredHeight(title.gameObject, S(16f));

        GameObject segmentsGO = new GameObject("Segments", typeof(RectTransform));
        segmentsGO.transform.SetParent(root.transform, false);
        HorizontalLayoutGroup hlg = segmentsGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = S(2f);
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        SetPreferredHeight(segmentsGO, S(10f));

        for (int i = 0; i < MaxSegments; i++)
        {
            GameObject seg = new GameObject($"Seg{i}", typeof(RectTransform));
            seg.transform.SetParent(segmentsGO.transform, false);
            Image segImg = seg.AddComponent<Image>();
            segImg.color = SegmentEmptyColor;
            LayoutElement segLe = seg.AddComponent<LayoutElement>();
            segLe.preferredWidth = S(9f);
            segLe.preferredHeight = S(6f);
            segLe.minWidth = S(9f);
            segLe.minHeight = S(6f);
        }

        TextMeshProUGUI cost = CreateText(root.transform, "0G", S(12), FontStyles.Bold, TextAlignmentOptions.Center);
        cost.color = GoldColor;
        SetPreferredHeight(cost.gameObject, S(18f));

        UpgradeCardUI cardUI = root.AddComponent<UpgradeCardUI>();
        var so = new SerializedObject(cardUI);
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("iconBackground").objectReferenceValue = iconBg;
        so.FindProperty("iconGlyph").objectReferenceValue = iconGlyph;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("costText").objectReferenceValue = cost;
        so.FindProperty("segmentsContainer").objectReferenceValue = segmentsGO.transform;
        so.FindProperty("button").objectReferenceValue = button;
        so.ApplyModifiedProperties();

        return root;
    }

    // ─── Shop Panel ─────────────────────────────────────────────────

    private static PermanentUpgradeShopUI BuildShopPanel(Transform canvasTransform, GameObject cardPrefabAsset)
    {
        GameObject root = new GameObject("PermanentUpgradeShop", typeof(RectTransform));
        root.transform.SetParent(canvasTransform, false);
        var rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Image dim = root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var panelRt = (RectTransform)panel.transform;
        // 콘텐츠가 1.5배로 커진 만큼 모달 프레임 자체도 넉넉하게 넓힌다.
        panelRt.anchorMin = new Vector2(0.08f, 0.06f);
        panelRt.anchorMax = new Vector2(0.92f, 0.94f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = PanelColor;

        VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(S(20), S(20), S(16), S(16));
        panelLayout.spacing = S(12f);
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        (TextMeshProUGUI goldText, Button closeButton) = BuildHeader(panel.transform);
        (RectTransform gridContent, PanelWidgets widgets) = BuildBody(panel.transform);

        PermanentUpgradeShopUI shopUI = root.AddComponent<PermanentUpgradeShopUI>();
        var so = new SerializedObject(shopUI);
        so.FindProperty("goldText").objectReferenceValue = goldText;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("gridContent").objectReferenceValue = gridContent;
        so.FindProperty("cardPrefab").objectReferenceValue = cardPrefabAsset.GetComponent<UpgradeCardUI>();
        so.FindProperty("detailIconBg").objectReferenceValue = widgets.DetailIconBg;
        so.FindProperty("detailIconGlyph").objectReferenceValue = widgets.DetailIconGlyph;
        so.FindProperty("detailTitle").objectReferenceValue = widgets.DetailTitle;
        so.FindProperty("detailDescription").objectReferenceValue = widgets.DetailDescription;
        so.FindProperty("detailCurrentValue").objectReferenceValue = widgets.DetailCurrentValue;
        so.FindProperty("detailArrow").objectReferenceValue = widgets.DetailArrow;
        so.FindProperty("detailNextValue").objectReferenceValue = widgets.DetailNextValue;
        so.FindProperty("detailBuyButton").objectReferenceValue = widgets.DetailBuyButton;
        so.FindProperty("detailBuyLabel").objectReferenceValue = widgets.DetailBuyLabel;
        so.FindProperty("statusText").objectReferenceValue = widgets.StatusText;
        so.ApplyModifiedProperties();

        root.SetActive(false);
        return shopUI;
    }

    private static (TextMeshProUGUI, Button) BuildHeader(Transform parent)
    {
        GameObject header = new GameObject("Header", typeof(RectTransform));
        header.transform.SetParent(parent, false);
        SetPreferredHeight(header, S(40f));
        HorizontalLayoutGroup layout = header.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = S(10f);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateText(header.transform, "영구 업그레이드", S(22), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        AddFlexibleWidth(title.gameObject);

        TextMeshProUGUI goldText = CreateText(header.transform, "0G", S(20), FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        goldText.color = GoldColor;
        SetPreferredWidth(goldText.gameObject, S(100f));

        Button closeButton = CreateButton(header.transform, "X", S(13));
        SetPreferredWidth(closeButton.gameObject, S(36f));
        SetPreferredHeight(closeButton.gameObject, S(36f));

        return (goldText, closeButton);
    }

    private static (RectTransform, PanelWidgets) BuildBody(Transform parent)
    {
        GameObject body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(parent, false);
        LayoutElement bodyLe = body.AddComponent<LayoutElement>();
        bodyLe.flexibleHeight = 1f;
        HorizontalLayoutGroup bodyLayout = body.AddComponent<HorizontalLayoutGroup>();
        bodyLayout.spacing = S(14f);
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = false;
        bodyLayout.childForceExpandHeight = true;

        RectTransform gridContent = BuildGrid(body.transform);
        PanelWidgets widgets = BuildDetailPanel(body.transform);

        return (gridContent, widgets);
    }

    private static RectTransform BuildGrid(Transform parent)
    {
        GameObject scrollFrame = new GameObject("GridScroll", typeof(RectTransform));
        scrollFrame.transform.SetParent(parent, false);
        LayoutElement scrollLe = scrollFrame.AddComponent<LayoutElement>();
        scrollLe.flexibleWidth = 1f;
        ScrollRect scrollRect = scrollFrame.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollFrame.transform, false);
        var viewportRt = (RectTransform)viewport.transform;
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        // Mask + 완전 투명(alpha 0) Image 조합은 스텐실 테스트가 전부 막혀 자식이 안 보이는
        // 유니티 UGUI 고질 버그가 있다 — Graphic이 필요 없는 RectMask2D로 대체.
        viewport.AddComponent<RectMask2D>();
        scrollRect.viewport = viewportRt;

        GameObject grid = new GameObject("Grid", typeof(RectTransform));
        grid.transform.SetParent(viewport.transform, false);
        var gridRt = (RectTransform)grid.transform;
        gridRt.anchorMin = new Vector2(0f, 1f);
        gridRt.anchorMax = new Vector2(1f, 1f);
        gridRt.pivot = new Vector2(0.5f, 1f);
        gridRt.offsetMin = Vector2.zero;
        gridRt.offsetMax = Vector2.zero;

        GridLayoutGroup gridLayout = grid.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(S(128f), S(148f));
        gridLayout.spacing = new Vector2(S(10f), S(10f));
        gridLayout.childAlignment = TextAnchor.UpperLeft;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 4;

        ContentSizeFitter fitter = grid.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = gridRt;

        return gridRt;
    }

    private static PanelWidgets BuildDetailPanel(Transform parent)
    {
        // DetailPanel 자체는 순수 폭 고정용 컨테이너로 남기고, VerticalLayoutGroup은
        // 자식(Content)에만 둔다 — 같은 오브젝트에 LayoutElement와 자식용 LayoutGroup이
        // 공존하면 부모(Body)가 폭을 재는 과정에서 서로 충돌해 고정폭이 무시된다.
        GameObject panel = new GameObject("DetailPanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        SetPreferredWidth(panel, S(260f));

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(panel.transform, false);
        var contentRt = (RectTransform)content.transform;
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;
        Image panelImg = content.AddComponent<Image>();
        panelImg.color = new Color(0.06f, 0.07f, 0.10f, 0.9f);

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(S(18), S(18), S(20), S(16));
        layout.spacing = S(10f);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        (Image detailIconBg, TextMeshProUGUI detailIconGlyph) = CreateIconCircle(content.transform, S(96f), S(22));

        TextMeshProUGUI detailTitle = CreateText(content.transform, "-", S(19), FontStyles.Bold, TextAlignmentOptions.Center);

        TextMeshProUGUI detailDescription = CreateText(content.transform, "", S(12), FontStyles.Normal, TextAlignmentOptions.Center);
        detailDescription.color = DescriptionColor;
        detailDescription.textWrappingMode = TextWrappingModes.Normal;
        SetPreferredHeight(detailDescription.gameObject, S(48f));

        GameObject valueRow = new GameObject("ValueRow", typeof(RectTransform));
        valueRow.transform.SetParent(content.transform, false);
        SetPreferredHeight(valueRow, S(30f));
        HorizontalLayoutGroup valueLayout = valueRow.AddComponent<HorizontalLayoutGroup>();
        valueLayout.childAlignment = TextAnchor.MiddleCenter;
        valueLayout.spacing = S(8f);
        valueLayout.childControlWidth = true;

        TextMeshProUGUI detailCurrentValue = CreateText(valueRow.transform, "-", S(16), FontStyles.Bold, TextAlignmentOptions.Center);
        TextMeshProUGUI detailArrow = CreateText(valueRow.transform, ">", S(16), FontStyles.Normal, TextAlignmentOptions.Center);
        detailArrow.color = DescriptionColor;
        TextMeshProUGUI detailNextValue = CreateText(valueRow.transform, "-", S(16), FontStyles.Bold, TextAlignmentOptions.Center);
        detailNextValue.color = NextValueColor;

        GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(content.transform, false);
        LayoutElement spacerLe = spacer.AddComponent<LayoutElement>();
        spacerLe.flexibleHeight = 1f;

        Button detailBuyButton = CreateButton(content.transform, "", S(13));
        SetPreferredHeight(detailBuyButton.gameObject, S(42f));
        TextMeshProUGUI detailBuyLabel = detailBuyButton.GetComponentInChildren<TextMeshProUGUI>();

        TextMeshProUGUI statusText = CreateText(content.transform, "", S(11), FontStyles.Normal, TextAlignmentOptions.Center);
        statusText.color = DescriptionColor;
        SetPreferredHeight(statusText.gameObject, S(24f));

        return new PanelWidgets
        {
            DetailIconBg = detailIconBg,
            DetailIconGlyph = detailIconGlyph,
            DetailTitle = detailTitle,
            DetailDescription = detailDescription,
            DetailCurrentValue = detailCurrentValue,
            DetailArrow = detailArrow,
            DetailNextValue = detailNextValue,
            DetailBuyButton = detailBuyButton,
            DetailBuyLabel = detailBuyLabel,
            StatusText = statusText,
        };
    }

    // ─── Lobby Shop Button ──────────────────────────────────────────

    private static void BuildLobbyShopButton(Transform canvasTransform, PermanentUpgradeShopUI shopUI)
    {
        GameObject go = new GameObject("LobbyShopButton", typeof(RectTransform));
        go.transform.SetParent(canvasTransform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        // 버튼이 커져도 우상단 앵커 코너(설정 버튼과의 상대 위치)는 그대로 유지 — 크기만 키운다.
        rt.anchoredPosition = new Vector2(-238.87f, -76f);
        rt.sizeDelta = new Vector2(S(148f), S(48f));

        Image img = go.AddComponent<Image>();
        img.color = ButtonColor;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        TextMeshProUGUI text = CreateText(go.transform, "상점", S(16), FontStyles.Bold, TextAlignmentOptions.Center);
        var textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        UnityEventTools.AddPersistentListener(btn.onClick, shopUI.OpenPanel);
    }

    // ─── 공용 헬퍼 ──────────────────────────────────────────────────

    private static (Image, TextMeshProUGUI) CreateIconCircle(Transform parent, float height, int fontSize)
    {
        GameObject go = new GameObject("Icon", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = CardIconBgColor;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        TextMeshProUGUI glyph = CreateText(go.transform, "?", fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        var glyphRt = glyph.rectTransform;
        glyphRt.anchorMin = Vector2.zero;
        glyphRt.anchorMax = Vector2.one;
        glyphRt.offsetMin = Vector2.zero;
        glyphRt.offsetMax = Vector2.zero;

        return (img, glyph);
    }

    private static TextMeshProUGUI CreateText(
        Transform parent, string text, int size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string label, int fontSize)
    {
        GameObject go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = ButtonColor;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        TextMeshProUGUI text = CreateText(go.transform, label, fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        var rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return btn;
    }

    // minHeight/minWidth도 함께 고정 — 같은 오브젝트에 자식용 LayoutGroup이 붙어 있으면
    // (Header의 HorizontalLayoutGroup 등) 그 그룹이 계산한 min 값이 우선순위 규칙상
    // LayoutElement의 preferred 값을 밀어낼 수 있어, min도 명시적으로 고정해 둔다.
    private static void SetPreferredHeight(GameObject go, float h)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredHeight = h;
        le.minHeight = h;
    }

    private static void SetPreferredWidth(GameObject go, float w)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.minWidth = w;
    }

    private static void AddFlexibleWidth(GameObject go)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
    }

    private struct PanelWidgets
    {
        public Image DetailIconBg;
        public TextMeshProUGUI DetailIconGlyph;
        public TextMeshProUGUI DetailTitle;
        public TextMeshProUGUI DetailDescription;
        public TextMeshProUGUI DetailCurrentValue;
        public TextMeshProUGUI DetailArrow;
        public TextMeshProUGUI DetailNextValue;
        public Button DetailBuyButton;
        public TextMeshProUGUI DetailBuyLabel;
        public TextMeshProUGUI StatusText;
    }
}
