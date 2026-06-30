using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.Player;

namespace Vamsurlike.UI
{
    // 다운된 플레이어 머리 위에 뜨는 구조 가능 시간 타이머 (1단계 IsDowned 전용).
    // 원형 Radial360 fill + 중앙 "Ns" 텍스트.
    // IsDeadWaiting(2단계) 에는 표시하지 않음.
    public sealed class WorldDownedTimerUI : NetworkBehaviour
    {
        [SerializeField] private Vector3 offset       = new Vector3(0f, 2.8f, 0f);
        [SerializeField] private float   canvasRadius = 80f;   // 원 지름 (pixels)
        [SerializeField] private float   worldScale   = 0.012f;
        [SerializeField] private float   downedDuration = 15f; // PlayerReviveHandler.downedDuration 과 맞춤
        [SerializeField] private Color   bgColor      = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        [SerializeField] private Color   fillColor    = new Color(0.9f,  0.25f, 0.1f,  1f);
        [SerializeField] private Color   textColor    = Color.white;

        private Transform       root;
        private Image           radialFill;
        private TextMeshProUGUI timerText;
        private CanvasGroup     cg;

        private PlayerNetworkStats  stats;
        private PlayerReviveHandler reviveHandler;

        public override void OnNetworkSpawn()
        {
            stats         = GetComponent<PlayerNetworkStats>();
            reviveHandler = GetComponent<PlayerReviveHandler>();

            BuildUI();

            if (stats != null)
                stats.IsDowned.OnValueChanged += OnIsDownedChanged;
            if (reviveHandler != null)
                reviveHandler.DownedTimeRemaining.OnValueChanged += OnTimerChanged;

            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            if (stats != null)
                stats.IsDowned.OnValueChanged -= OnIsDownedChanged;
            if (reviveHandler != null)
                reviveHandler.DownedTimeRemaining.OnValueChanged -= OnTimerChanged;
        }

        private void LateUpdate()
        {
            if (root == null || Camera.main == null) return;
            root.rotation = Camera.main.transform.rotation;
        }

        private void OnIsDownedChanged(bool _, bool __) => Refresh();

        private void OnTimerChanged(float _, float next)
        {
            UpdateFill(next);
            UpdateText(next);
        }

        private void Refresh()
        {
            bool isDowned = stats != null && stats.IsDowned.Value;
            if (cg != null) cg.alpha = isDowned ? 1f : 0f;
            if (isDowned && reviveHandler != null)
            {
                float remaining = reviveHandler.DownedTimeRemaining.Value;
                UpdateFill(remaining);
                UpdateText(remaining);
            }
        }

        private void UpdateFill(float remaining)
        {
            if (radialFill == null) return;
            radialFill.fillAmount = downedDuration > 0f
                ? Mathf.Clamp01(remaining / downedDuration)
                : 0f;
        }

        private void UpdateText(float remaining)
        {
            if (timerText == null) return;
            timerText.text = $"{remaining:0}s";
        }

        private void BuildUI()
        {
            float size = canvasRadius;

            var go = new GameObject("DownedTimer");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            go.transform.localScale    = Vector3.one * worldScale;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);

            cg       = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            root     = go.transform;

            // 배경 원
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(go.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color      = bgColor;
            bgImg.type       = Image.Type.Filled;
            bgImg.fillMethod = Image.FillMethod.Radial360;
            bgImg.fillAmount = 1f;
            if (bgImg.sprite == null)
                bgImg.sprite = FilledImageUtility.GetOrCreateFallbackSprite();
            Stretch(bgGO);

            // Radial fill (남은 시간 비율)
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(go.transform, false);
            radialFill              = fillGO.AddComponent<Image>();
            radialFill.color        = fillColor;
            radialFill.type         = Image.Type.Filled;
            radialFill.fillMethod   = Image.FillMethod.Radial360;
            radialFill.fillOrigin   = (int)Image.Origin360.Top;
            radialFill.fillClockwise = true;
            radialFill.fillAmount   = 1f;
            if (radialFill.sprite == null)
                radialFill.sprite = FilledImageUtility.GetOrCreateFallbackSprite();
            Stretch(fillGO);

            // 중앙 텍스트 "Ns"
            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            timerText           = txtGO.AddComponent<TextMeshProUGUI>();
            timerText.text      = "";
            timerText.fontSize  = 28f;
            timerText.color     = textColor;
            timerText.fontStyle = FontStyles.Bold;
            timerText.alignment = TextAlignmentOptions.Center;
            Stretch(txtGO);
        }

        private static void Stretch(GameObject go)
        {
            var r        = go.GetComponent<RectTransform>();
            r.anchorMin  = Vector2.zero;
            r.anchorMax  = Vector2.one;
            r.offsetMin  = Vector2.zero;
            r.offsetMax  = Vector2.zero;
        }
    }
}
