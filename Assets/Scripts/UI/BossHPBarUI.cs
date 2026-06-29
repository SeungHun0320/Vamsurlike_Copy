using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // 보스 HP 바 — 화면 최상단 풀 폭 배치.
    // segmentCount 단위로 HP를 나눠 각 단계마다 fill 색상을 변경.
    // 세그먼트 경계선은 Show() 최초 호출 시 동적으로 생성한다.
    public sealed class BossHPBarUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject      panel;
        [SerializeField] private Image           hpFill;
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("Segments")]
        [SerializeField] private int     segmentCount = 10;
        [SerializeField] private Color[] tierColors   = new Color[]
        {
            new Color(0.95f, 0.15f, 0.15f, 1f),  // 빨강  (90-100%)
            new Color(1.00f, 0.45f, 0.05f, 1f),  // 주황  (60-90%)
            new Color(1.00f, 0.85f, 0.10f, 1f),  // 노랑  (30-60%)
            new Color(0.75f, 0.15f, 0.90f, 1f),  // 보라  (0-30%)
        };

        [Header("Dividers")]
        [SerializeField] private bool  showDividers = true;
        [SerializeField] private Color dividerColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private float dividerWidth = 2f;

        [Header("Format")]
        [SerializeField] private string hpFormat = "{0:0} / {1:0}";

        private BossHPBarViewModel viewModel;
        private CanvasGroup        panelCanvasGroup;
        private int                lastSegment  = -1;
        private bool               dividersBuilt;

        private void Awake()
        {
            if (panel == null) panel = gameObject;
            panelCanvasGroup = panel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
                panelCanvasGroup = panel.AddComponent<CanvasGroup>();

            FilledImageUtility.ConfigureHorizontal(hpFill);
            viewModel = new BossHPBarViewModel();
            SetVisible(false);
        }

        private void OnEnable()
        {
            viewModel.OnBossVisible += Show;
            viewModel.OnBossHidden  += Hide;
            viewModel.Bind();
        }

        private void OnDisable()
        {
            viewModel.OnBossVisible -= Show;
            viewModel.OnBossHidden  -= Hide;
            viewModel.Unbind();
        }

        private void Show(BossStatusPayload p)
        {
            SetVisible(true);

            float maxHp = p.MaxHp > 0f ? p.MaxHp : 1f;
            float norm  = Mathf.Clamp01(p.Hp / maxHp);

            FilledImageUtility.SetAmount(hpFill, norm);
            if (hpText != null) hpText.text = string.Format(hpFormat, p.Hp, p.MaxHp);

            UpdateTierColor(norm);

            if (showDividers && !dividersBuilt)
                BuildDividers();
        }

        private void Hide() => SetVisible(false);

        // ── 세그먼트 색 변경 ───────────────────────────────────

        private void UpdateTierColor(float normalizedHp)
        {
            if (hpFill == null || tierColors == null || tierColors.Length == 0) return;

            int segment = Mathf.Clamp(
                Mathf.FloorToInt(normalizedHp * segmentCount),
                0, segmentCount - 1);

            if (segment == lastSegment) return;
            lastSegment = segment;

            // segment가 낮을수록(HP 적을수록) tierColors 뒤쪽 색상 사용
            int colorIndex = Mathf.Clamp(
                Mathf.FloorToInt((1f - (float)segment / segmentCount) * tierColors.Length),
                0, tierColors.Length - 1);

            hpFill.color = tierColors[colorIndex];
        }

        // ── 세그먼트 구분선 ────────────────────────────────────

        private void BuildDividers()
        {
            if (hpFill == null) return;
            dividersBuilt = true;

            var container = new GameObject("SegmentDividers").AddComponent<RectTransform>();
            container.transform.SetParent(hpFill.transform.parent, false);
            container.anchorMin        = Vector2.zero;
            container.anchorMax        = Vector2.one;
            container.sizeDelta        = Vector2.zero;
            container.anchoredPosition = Vector2.zero;

            for (int i = 1; i < segmentCount; i++)
            {
                float t   = (float)i / segmentCount;
                var divGO = new GameObject($"Div_{i}");
                divGO.transform.SetParent(container.transform, false);

                var img   = divGO.AddComponent<Image>();
                img.color = dividerColor;

                var rt = divGO.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(t, 0f);
                rt.anchorMax        = new Vector2(t, 1f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.sizeDelta        = new Vector2(dividerWidth, 0f);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        // ── 패널 표시 제어 ─────────────────────────────────────

        private void SetVisible(bool visible)
        {
            if (panel == null) return;
            if (!panel.activeSelf) panel.SetActive(true);

            if (panelCanvasGroup == null)
                panelCanvasGroup = panel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null) return;

            panelCanvasGroup.alpha          = visible ? 1f : 0f;
            panelCanvasGroup.interactable   = visible;
            panelCanvasGroup.blocksRaycasts = visible;
        }
    }
}
