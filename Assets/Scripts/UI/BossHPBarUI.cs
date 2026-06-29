using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // 세그먼트형 보스 HP 바.
    // 전체 HP를 segmentCount 덩어리로 나누고, 큰 바 하나가 한 덩어리를 표시한다.
    // 한 덩어리가 소진되면 바가 즉시 꽉 찬 상태로 리셋되며 색이 바뀐다.
    // 남은 덩어리 수는 chunkText와 pip 아이콘으로 표시.
    public sealed class BossHPBarUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject      panel;
        [SerializeField] private Image           hpFill;
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("Segments")]
        [SerializeField] private int     segmentCount = 5;
        [SerializeField] private Color[] chunkColors  = new Color[]
        {
            new Color(0.95f, 0.20f, 0.15f, 1f),  // chunk 1 (마지막, 빨강)
            new Color(1.00f, 0.50f, 0.05f, 1f),  // chunk 2
            new Color(1.00f, 0.80f, 0.10f, 1f),  // chunk 3
            new Color(0.70f, 0.20f, 0.90f, 1f),  // chunk 4
            new Color(0.40f, 0.10f, 0.85f, 1f),  // chunk 5 (처음, 보라)
        };

        [Header("Pip Counter")]
        [SerializeField] private float pipSize    = 14f;
        [SerializeField] private float pipSpacing = 4f;
        [SerializeField] private Color pipActive  = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color pipDepleted= new Color(0.3f, 0.3f, 0.3f, 0.5f);

        [Header("Format")]
        [SerializeField] private string hpFormat = "{0:0} / {1:0}";

        private BossHPBarViewModel viewModel;
        private CanvasGroup        panelCanvasGroup;
        private Image[]            pips;
        private int                lastChunk = -1;
        private bool               visualsBuilt;

        private void Awake()
        {
            if (panel == null) panel = gameObject;
            panelCanvasGroup = panel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
                panelCanvasGroup = panel.AddComponent<CanvasGroup>();

            if (hpFill != null)
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

            if (!visualsBuilt) BuildPips();

            UpdateBar(p.NormalizedHp);

            if (hpText != null)
                hpText.text = string.Format(hpFormat, p.Hp, p.MaxHp);
        }

        private void Hide() => SetVisible(false);

        // ── 바 업데이트 ───────────────────────────────────────

        private void UpdateBar(float normalizedHp)
        {
            if (hpFill == null) return;

            if (normalizedHp <= 0f)
            {
                hpFill.fillAmount = 0f;
                UpdatePips(0);
                return;
            }

            // 현재 덩어리 인덱스 (0 = 마지막 덩어리, segmentCount-1 = 첫 덩어리)
            int chunk = Mathf.Clamp(
                Mathf.FloorToInt(normalizedHp * segmentCount),
                0, segmentCount - 1);

            // 해당 덩어리 안에서의 진행도 (0→1)
            float progress = normalizedHp * segmentCount - chunk;

            hpFill.fillAmount = progress;

            // 덩어리가 바뀔 때만 색 업데이트
            if (chunk != lastChunk)
            {
                lastChunk = chunk;
                // chunkColors 인덱스: 처음 덩어리(segmentCount-1)가 마지막 색상
                int colorIdx = Mathf.Clamp(segmentCount - 1 - chunk, 0, chunkColors.Length - 1);
                hpFill.color = chunkColors[colorIdx];
            }

            // chunk + 1 = 남은 덩어리 수
            UpdatePips(chunk + 1);
        }

        // ── Pip 생성 / 업데이트 ───────────────────────────────

        private void BuildPips()
        {
            visualsBuilt = true;
            if (panel == null) return;

            pips = new Image[segmentCount];
            float totalWidth = segmentCount * pipSize + (segmentCount - 1) * pipSpacing;
            float startX     = -totalWidth * 0.5f + pipSize * 0.5f;

            for (int i = 0; i < segmentCount; i++)
            {
                var go  = new GameObject($"Pip{i}");
                go.transform.SetParent(panel.transform, false);

                var img = go.AddComponent<Image>();
                img.color = pipActive;

                var rt = go.GetComponent<RectTransform>();
                // 패널 상단 중앙에 가로로 나열
                rt.anchorMin        = new Vector2(0.5f, 1f);
                rt.anchorMax        = new Vector2(0.5f, 1f);
                rt.pivot            = new Vector2(0.5f, 1f);
                rt.sizeDelta        = new Vector2(pipSize, pipSize * 0.5f);
                rt.anchoredPosition = new Vector2(
                    startX + i * (pipSize + pipSpacing),
                    -2f);

                pips[i] = img;
            }

            if (hpFill  != null) hpFill.transform.SetAsFirstSibling();
            if (hpText  != null) hpText.transform.SetAsLastSibling();
        }

        private void UpdatePips(int remaining)
        {
            if (pips == null) return;
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                // pip 0 = 마지막 덩어리, pip N-1 = 첫 덩어리
                // remaining 개수만큼 활성화 (왼쪽부터)
                pips[i].color = i < remaining ? pipActive : pipDepleted;
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
