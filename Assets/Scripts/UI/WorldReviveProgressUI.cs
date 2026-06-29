using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI
{
    // 다운 상태에서 팀원이 부활 중일 때 플레이어 머리 위에 떠오르는 월드 스페이스 진행도 바.
    // IsOwner(로컬 플레이어)일 때만 캔버스를 생성하고 이벤트에 구독한다.
    public sealed class WorldReviveProgressUI : NetworkBehaviour
    {
        [SerializeField] private Vector3 offset      = new Vector3(0f, 2.5f, 0f);
        [SerializeField] private Vector2 canvasSize  = new Vector2(300f, 48f);
        [SerializeField] private float   worldScale  = 0.01f;
        [SerializeField] private Color   fillColor   = new Color(0.2f, 0.8f, 0.4f, 1f);

        private Transform       barRoot;
        private Image           progressFill;
        private TextMeshProUGUI progressText;
        private CanvasGroup     cg;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            BuildBar();
            if (UIEventHub.Instance != null)
                UIEventHub.Instance.Player.ReviveProgressChanged += OnReviveProgress;
        }

        public override void OnNetworkDespawn()
        {
            if (UIEventHub.Instance != null)
                UIEventHub.Instance.Player.ReviveProgressChanged -= OnReviveProgress;
        }

        // 빌보드: 카메라를 향해 회전
        private void LateUpdate()
        {
            if (barRoot == null || !IsOwner) return;
            if (Camera.main != null)
                barRoot.rotation = Camera.main.transform.rotation;
        }

        private void OnReviveProgress(ReviveProgressPayload p)
        {
            if (cg == null) return;

            if (p.Progress < 0f)
            {
                cg.alpha = 0f;
                return;
            }

            cg.alpha = 1f;
            if (progressFill != null) progressFill.fillAmount = p.Progress;
            if (progressText != null) progressText.text = $"부활 중... {p.Progress * 100f:0}%";
        }

        private void BuildBar()
        {
            var go = new GameObject("ReviveProgressBar");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            go.transform.localScale    = Vector3.one * worldScale;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 1f;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = canvasSize;

            cg        = go.AddComponent<CanvasGroup>();
            cg.alpha  = 0f;
            barRoot   = go.transform;

            // 배경
            var bgGO  = new GameObject("BG");
            bgGO.transform.SetParent(go.transform, false);
            bgGO.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
            Stretch(bgGO);

            // 채우기 바
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(go.transform, false);
            progressFill            = fillGO.AddComponent<Image>();
            progressFill.color      = fillColor;
            progressFill.type       = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = 0f;
            Stretch(fillGO);

            // 텍스트
            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            progressText           = txtGO.AddComponent<TextMeshProUGUI>();
            progressText.text      = "부활 중...";
            progressText.fontSize  = 22f;
            progressText.color     = Color.white;
            progressText.fontStyle = FontStyles.Bold;
            progressText.alignment = TextAlignmentOptions.Center;
            Stretch(txtGO);
        }

        private static void Stretch(GameObject go)
        {
            var rt        = go.GetComponent<RectTransform>();
            rt.anchorMin  = Vector2.zero;
            rt.anchorMax  = Vector2.one;
            rt.sizeDelta  = Vector2.zero;
            rt.offsetMin  = Vector2.zero;
            rt.offsetMax  = Vector2.zero;
        }
    }
}
