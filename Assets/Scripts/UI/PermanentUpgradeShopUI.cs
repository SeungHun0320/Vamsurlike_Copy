using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vamsurlike.UI.ViewModels;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI
{
    // 레퍼런스: LoL "Swarm" PVE 모드의 PURCHASE UPGRADES 상점 (그리드 카드 + 세그먼트 레벨 바 + 우측 상세 패널).
    // 하이어라키는 에디터에서 미리 만들어 두고, 이 스크립트는 그 하이어라키를 바인딩만 한다.
    // 그리드 카드만 데이터 개수만큼 cardPrefab을 Instantiate한다 (다른 Row 프리팹들과 동일한 패턴).
    public class PermanentUpgradeShopUI : MonoBehaviour
    {
        [Header("Debug Hotkeys")]
        [SerializeField] private bool enableDebugHotkeys = true;
        [SerializeField] private Key debugGrantGoldKey = Key.F9;
        [SerializeField] private Key debugPurchaseSelectedKey = Key.F10;
        [SerializeField] private Key debugApplyKey = Key.F11;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private Button          closeButton;

        [Header("Grid")]
        [SerializeField] private Transform     gridContent;
        [SerializeField] private UpgradeCardUI cardPrefab;

        [Header("Detail Panel")]
        [SerializeField] private Image           detailIconBg;
        [SerializeField] private TextMeshProUGUI detailIconGlyph;
        [SerializeField] private TextMeshProUGUI detailTitle;
        [SerializeField] private TextMeshProUGUI detailDescription;
        [SerializeField] private TextMeshProUGUI detailCurrentValue;
        [SerializeField] private TextMeshProUGUI detailArrow;
        [SerializeField] private TextMeshProUGUI detailNextValue;
        [SerializeField] private Button          detailBuyButton;
        [SerializeField] private TextMeshProUGUI detailBuyLabel;
        [SerializeField] private TextMeshProUGUI statusText;

        private static readonly Color CardIconBgColor = new(0.16f, 0.17f, 0.20f, 1f);
        private static readonly Color NextValueColor  = new(0.42f, 0.90f, 0.55f, 1f);

        private readonly List<UpgradeCardUI> cards = new();
        private PermanentUpgradeShopViewModel viewModel;

        private void Awake()
        {
            viewModel = new PermanentUpgradeShopViewModel();
            if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
            if (detailBuyButton != null) detailBuyButton.onClick.AddListener(OnBuyClicked);
        }

        public void OpenPanel() => gameObject.SetActive(true);
        private void ClosePanel() => gameObject.SetActive(false);

        private void OnEnable()
        {
            viewModel.Changed += Refresh;
            viewModel.Bind();
        }

        private void OnDisable()
        {
            viewModel.Changed -= Refresh;
            viewModel.Dispose();
        }

        private void Update()
        {
            if (!enableDebugHotkeys) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame) ClosePanel();

            if (keyboard[debugGrantGoldKey].wasPressedThisFrame)
            {
                viewModel.DebugGrantGold(500);
                SetStatus("F9: 골드 +500");
            }

            if (keyboard[debugPurchaseSelectedKey].wasPressedThisFrame && viewModel.SelectedType.HasValue)
                OnBuyClicked();

            if (keyboard[debugApplyKey].wasPressedThisFrame)
            {
                SetStatus(viewModel.ApplyToLocalPlayer()
                    ? "F11: 현재 플레이어에 적용 요청"
                    : "F11: 플레이어 없음 — 다음 스테이지 시작 시 자동 적용");
            }
        }

        // ─── Refresh ────────────────────────────────────────────────────

        private void Refresh()
        {
            if (goldText != null) goldText.text = $"{viewModel.Gold}G";

            IReadOnlyList<PermanentUpgradeShopRow> rows = viewModel.Rows;
            while (cards.Count < rows.Count)
                cards.Add(Instantiate(cardPrefab, gridContent));

            for (int i = 0; i < cards.Count; i++)
            {
                bool active = i < rows.Count;
                cards[i].gameObject.SetActive(active);
                if (active) cards[i].Bind(rows[i], viewModel.SelectedType == rows[i].Type, viewModel.SelectDetail);
            }

            RefreshDetail();
        }

        private void RefreshDetail()
        {
            if (!viewModel.TryGetDetail(out PermanentUpgradeDetail detail))
            {
                if (detailTitle != null) detailTitle.text = "-";
                if (detailDescription != null) detailDescription.text = string.Empty;
                if (detailBuyButton != null) detailBuyButton.interactable = false;
                return;
            }

            if (detailIconBg != null) detailIconBg.color = CardIconBgColor;
            if (detailIconGlyph != null)
            {
                detailIconGlyph.text = PermanentUpgradeDisplay.GetIconGlyph(detail.Type);
                detailIconGlyph.color = PermanentUpgradeDisplay.GetIconColor(detail.Type);
            }
            if (detailTitle != null) detailTitle.text = detail.Name;
            if (detailDescription != null) detailDescription.text = detail.Description;
            if (detailCurrentValue != null) detailCurrentValue.text = detail.CurrentValueText;

            bool showNext = !detail.IsMaxed;
            if (detailArrow != null) detailArrow.gameObject.SetActive(showNext);
            if (detailNextValue != null)
            {
                detailNextValue.gameObject.SetActive(showNext);
                if (showNext) detailNextValue.text = detail.NextValueText;
            }

            if (detailBuyButton != null) detailBuyButton.interactable = detail.CanPurchase;
            if (detailBuyLabel != null) detailBuyLabel.text = detail.IsMaxed ? "MAX" : $"구매 ({detail.Cost}G)";
        }

        private void OnBuyClicked()
        {
            if (!viewModel.SelectedType.HasValue) return;
            SetStatus(viewModel.TryPurchase(viewModel.SelectedType.Value) ? "구매 완료" : "구매 실패");
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
