using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // 세그먼트형 보스 HP 바 (MapleStory / Lost Ark 스타일).
    // 큰 바 하나 = 한 덩어리. 소진 시 즉시 리셋 + 색 변경.
    // fill 뒤에 다음 덩어리 색이 ghost로 미리 보임.
    public sealed class BossHPBarUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject      panel;
        [SerializeField] private Image           hpFill;
        [SerializeField] private TextMeshProUGUI hpText;   // 런타임에 "보스명  ×N"으로 교체

        [Header("Boss Info")]
        [SerializeField] private string bossName = "BOSS";

        [Header("Segments")]
        [SerializeField] private int      segmentCount   = 10;
        // t=0 → 첫 덩어리 색, t=1 → 마지막 덩어리 색
        [SerializeField] private Gradient chunkGradient;

        [Header("Pip Counter")]
        [SerializeField] private float pipSize     = 10f;
        [SerializeField] private float pipSpacing  = 3f;
        [SerializeField] private Color pipActive   = new Color(1f,   1f,   1f,   0.85f);
        [SerializeField] private Color pipDepleted = new Color(0.3f, 0.3f, 0.3f, 0.50f);

        private BossHPBarViewModel viewModel;
        private CanvasGroup        panelCanvasGroup;
        private Image              hpGhost;   // 다음 덩어리 색 미리보기
        private Image[]            pips;
        private int                lastChunk  = -1;
        private int                lastRemain = -1;
        private bool               visualsBuilt;

        // ── 생명주기 ──────────────────────────────────────────

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

        // ── Show / Hide ───────────────────────────────────────

        private void Show(BossStatusPayload p)
        {
            SetVisible(true);
            if (!visualsBuilt) BuildVisuals();
            UpdateBar(p.NormalizedHp);
        }

        private void Hide()
        {
            SetVisible(false);
            lastChunk  = -1;
            lastRemain = -1;
        }

        // ── 바 업데이트 ───────────────────────────────────────

        private void UpdateBar(float normalizedHp)
        {
            if (hpFill == null) return;

            if (normalizedHp <= 0f)
            {
                hpFill.fillAmount = 0f;
                if (hpGhost != null) hpGhost.color = Color.clear;
                SetLabelText(0);
                UpdatePips(0);
                return;
            }

            int chunk    = Mathf.Clamp(Mathf.FloorToInt(normalizedHp * segmentCount), 0, segmentCount - 1);
            float progress = normalizedHp * segmentCount - chunk;

            hpFill.fillAmount = progress;

            if (chunk != lastChunk)
            {
                lastChunk = chunk;
                hpFill.color = GetChunkColor(chunk);

                // ghost = 다음(chunk-1) 덩어리 색
                if (hpGhost != null)
                    hpGhost.color = chunk > 0 ? GetChunkColor(chunk - 1) : Color.clear;
            }

            int remaining = chunk + 1;
            SetLabelText(remaining);
            UpdatePips(remaining);
        }

        private Color GetChunkColor(int chunkIndex)
        {
            if (chunkGradient == null) return Color.red;
            float t = 1f - (float)chunkIndex / Mathf.Max(1, segmentCount - 1);
            return chunkGradient.Evaluate(t);
        }

        // ── 텍스트 ────────────────────────────────────────────

        private void SetLabelText(int remaining)
        {
            if (hpText == null) return;
            if (lastRemain == remaining) return;
            lastRemain = remaining;
            hpText.text = remaining > 0 ? $"{bossName}  ×{remaining}" : bossName;
        }

        // ── Pip ──────────────────────────────────────────────

        private void UpdatePips(int remaining)
        {
            if (pips == null) return;
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                pips[i].color = i < remaining ? pipActive : pipDepleted;
            }
        }

        // ── 비주얼 빌드 (보스 최초 등장 시) ──────────────────

        private void BuildVisuals()
        {
            visualsBuilt = true;

            // 1. Ghost bar — hpFill 바로 앞에 삽입 (렌더링 순서: ghost → fill → 텍스트)
            if (hpFill != null)
            {
                var ghostGO = new GameObject("HpGhost");
                ghostGO.transform.SetParent(panel.transform, false);
                hpGhost = ghostGO.AddComponent<Image>();
                hpGhost.color = Color.clear;

                var rt = ghostGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                ghostGO.transform.SetSiblingIndex(hpFill.transform.GetSiblingIndex());
            }

            // 2. Pip 아이콘 (패널 상단, 바 바깥)
            pips = new Image[segmentCount];
            float totalW = segmentCount * pipSize + (segmentCount - 1) * pipSpacing;
            float startX = -totalW * 0.5f + pipSize * 0.5f;

            for (int i = 0; i < segmentCount; i++)
            {
                var go  = new GameObject($"Pip{i}");
                go.transform.SetParent(panel.transform, false);
                var img = go.AddComponent<Image>();
                img.color = pipActive;

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0.5f, 1f);
                rt.anchorMax        = new Vector2(0.5f, 1f);
                rt.pivot            = new Vector2(0.5f, 0f);
                rt.sizeDelta        = new Vector2(pipSize, pipSize * 0.4f);
                rt.anchoredPosition = new Vector2(startX + i * (pipSize + pipSpacing), 3f);

                pips[i] = img;
            }

            // 3. hpText → 바 위에 겹쳐서 "보스명  ×N" 표시
            if (hpText != null)
            {
                hpText.fontSize  = 32f;
                hpText.fontStyle = FontStyles.Bold;
                hpText.color     = Color.white;
                hpText.alignment = TextAlignmentOptions.Midline;

                var rt = hpText.GetComponent<RectTransform>();
                rt.anchorMin        = Vector2.zero;
                rt.anchorMax        = Vector2.one;
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.sizeDelta        = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                hpText.transform.SetAsLastSibling();
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
