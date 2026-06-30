using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.Player;

namespace Vamsurlike.UI
{
    // 다운된 플레이어 머리 위에 뜨는 구조 가능 시간 타이머 (1단계 IsDowned 전용).
    // IsDeadWaiting(2단계) 에는 표시하지 않음.
    // NetworkVariableReadPermission.Everyone 이라 모든 클라이언트가 직접 읽음.
    public sealed class WorldDownedTimerUI : NetworkBehaviour
    {
        [SerializeField] private Vector3 offset    = new Vector3(0f, 2.8f, 0f);
        [SerializeField] private Vector2 canvasSize = new Vector2(220f, 48f);
        [SerializeField] private float   worldScale = 0.01f;
        [SerializeField] private Color   bgColor   = new Color(0.8f, 0.1f, 0.05f, 0.75f);
        [SerializeField] private Color   textColor = Color.white;

        private Transform       barRoot;
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
            if (barRoot == null || Camera.main == null) return;
            barRoot.rotation = Camera.main.transform.rotation;
        }

        private void OnIsDownedChanged(bool _, bool __)  => Refresh();
        private void OnTimerChanged(float _, float next) => UpdateText(next);

        private void Refresh()
        {
            bool isDowned = stats != null && stats.IsDowned.Value;
            if (cg != null) cg.alpha = isDowned ? 1f : 0f;
            if (isDowned && reviveHandler != null)
                UpdateText(reviveHandler.DownedTimeRemaining.Value);
        }

        private void UpdateText(float remaining)
        {
            if (timerText == null) return;
            timerText.text = $"구조 가능  {remaining:0}s";
        }

        private void BuildUI()
        {
            var go = new GameObject("DownedTimer");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            go.transform.localScale    = Vector3.one * worldScale;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = canvasSize;

            cg       = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            barRoot  = go.transform;

            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(go.transform, false);
            bgGO.AddComponent<Image>().color = bgColor;
            Stretch(bgGO);

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            timerText           = txtGO.AddComponent<TextMeshProUGUI>();
            timerText.text      = "";
            timerText.fontSize  = 22f;
            timerText.color     = textColor;
            timerText.fontStyle = FontStyles.Bold;
            timerText.alignment = TextAlignmentOptions.Center;
            Stretch(txtGO);
        }

        private static void Stretch(GameObject go)
        {
            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
