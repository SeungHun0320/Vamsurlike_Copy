using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // 보스 HP를 N개 줄로 분할해 동시에 표시 (MapleStory / Lost Ark 스타일).
    // 각 줄은 총 HP의 1/segmentCount 에 해당하며, 위 줄부터 먼저 깎인다.
    // 줄들은 Show() 최초 호출 시 panel 안에 동적으로 생성된다.
    public sealed class BossHPBarUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject      panel;
        [SerializeField] private Image           hpFill;   // CreateHUD 호환용 — 런타임에 숨김
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("Segments")]
        [SerializeField] private int   segmentCount = 5;
        [SerializeField] private float gapFraction  = 0.15f;
        [SerializeField] private Color segBgColor   = new Color(0.12f, 0.04f, 0.04f, 0.9f);
        [SerializeField] private Color segFillColor = new Color(0.95f, 0.25f, 0.15f, 1f);

        [Header("Format")]
        [SerializeField] private string hpFormat = "{0:0} / {1:0}";

        private BossHPBarViewModel viewModel;
        private CanvasGroup        panelCanvasGroup;
        private Image[]            segFills;
        private bool               barsBuilt;

        private void Awake()
        {
            if (panel == null) panel = gameObject;
            panelCanvasGroup = panel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
                panelCanvasGroup = panel.AddComponent<CanvasGroup>();

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

            if (!barsBuilt) BuildSegmentBars();

            UpdateFills(p.NormalizedHp);

            if (hpText != null)
                hpText.text = string.Format(hpFormat, p.Hp, p.MaxHp);
        }

        private void Hide() => SetVisible(false);

        // ── 줄 생성 ───────────────────────────────────────────

        private void BuildSegmentBars()
        {
            barsBuilt = true;

            // CreateHUD가 만든 단일 fill 이미지를 숨긴다
            if (hpFill != null) hpFill.gameObject.SetActive(false);

            segFills = new Image[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                // i=0 이 맨 아래(마지막에 깎임), i=segmentCount-1 이 맨 위(먼저 깎임)
                float yMin    = (float)i / segmentCount;
                float yMax    = (float)(i + 1) / segmentCount;
                float halfGap = (yMax - yMin) * gapFraction * 0.5f;

                // 배경
                var bgGO = new GameObject($"Seg{i}Bg");
                bgGO.transform.SetParent(panel.transform, false);
                var bg = bgGO.AddComponent<Image>();
                bg.color = segBgColor;

                var bgRt = bgGO.GetComponent<RectTransform>();
                bgRt.anchorMin        = new Vector2(0f, yMin + halfGap);
                bgRt.anchorMax        = new Vector2(1f, yMax - halfGap);
                bgRt.pivot            = new Vector2(0.5f, 0.5f);
                bgRt.sizeDelta        = Vector2.zero;
                bgRt.anchoredPosition = Vector2.zero;

                // 채움
                var fillGO = new GameObject($"Seg{i}Fill");
                fillGO.transform.SetParent(bgGO.transform, false);
                var fill = fillGO.AddComponent<Image>();
                fill.color = segFillColor;
                FilledImageUtility.ConfigureHorizontal(fill);

                var fillRt = fillGO.GetComponent<RectTransform>();
                fillRt.anchorMin        = Vector2.zero;
                fillRt.anchorMax        = Vector2.one;
                fillRt.pivot            = new Vector2(0.5f, 0.5f);
                fillRt.sizeDelta        = Vector2.zero;
                fillRt.anchoredPosition = Vector2.zero;

                segFills[i] = fill;
            }

            // 텍스트가 줄 위에 렌더링되도록 최상위 sibling으로 이동
            if (hpText != null)
                hpText.transform.SetAsLastSibling();
        }

        // ── 채움 업데이트 ─────────────────────────────────────

        private void UpdateFills(float normalizedHp)
        {
            if (segFills == null) return;
            for (int i = 0; i < segFills.Length; i++)
            {
                if (segFills[i] == null) continue;
                // 줄 i 는 HP 범위 [i/N, (i+1)/N] 을 담당
                // 0 → 완전 비어있음(이미 깎임), 1 → 꽉 참
                segFills[i].fillAmount = Mathf.Clamp01(normalizedHp * segmentCount - i);
            }
        }

        // ── 패널 표시 제어 ────────────────────────────────────

        private void SetVisible(bool visible)
        {
            if (panel == null) return;
            if (!panel.activeSelf) panel.SetActive(true);
            if (panelCanvasGroup == null) return;
            panelCanvasGroup.alpha          = visible ? 1f : 0f;
            panelCanvasGroup.interactable   = visible;
            panelCanvasGroup.blocksRaycasts = visible;
        }
    }
}
