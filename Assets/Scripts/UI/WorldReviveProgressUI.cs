using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI
{
    // 다운된 플레이어 머리 위에 떠오르는 월드 스페이스 부활 진행도 바.
    // 모든 클라이언트 인스턴스가 이벤트를 수신하되, DownedClientId == OwnerClientId 인 경우에만 표시.
    // 구조체에 DownedClientId가 있으므로 IsOwner 제한 없이 다운된 플레이어 위에 정확히 렌더됨.
    public sealed class WorldReviveProgressUI : NetworkBehaviour
    {
        [SerializeField] private Vector3 offset     = new Vector3(0f, 2.5f, 0f);
        [SerializeField] private Vector2 canvasSize = new Vector2(300f, 48f);
        [SerializeField] private float   worldScale = 0.01f;
        [SerializeField] private Color   fillColor  = new Color(0.2f, 0.8f, 0.4f, 1f);

        private Transform       barRoot;
        private Image           progressFill;
        private TextMeshProUGUI progressText;
        private CanvasGroup     cg;

        public override void OnNetworkSpawn()
        {
            BuildBar();

            if (UIEventHub.Instance != null)
            {
                UIEventHub.Instance.Player.ReviveProgressChanged += OnReviveProgress;
                UIEventHub.Instance.Player.PlayerRevived         += HideBar;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (UIEventHub.Instance != null)
            {
                UIEventHub.Instance.Player.ReviveProgressChanged -= OnReviveProgress;
                UIEventHub.Instance.Player.PlayerRevived         -= HideBar;
            }
        }

        // 빌보드: 카메라를 향해 회전
        private void LateUpdate()
        {
            if (barRoot == null || Camera.main == null) return;
            barRoot.rotation = Camera.main.transform.rotation;
        }

        private void OnReviveProgress(ReviveProgressPayload p)
        {
            // 이 인스턴스가 다운된 플레이어인 경우에만 표시
            if (p.DownedClientId != OwnerClientId) return;

            if (p.Progress < 0f) { HideBar(); return; }

            if (cg != null) cg.alpha = 1f;
            if (progressFill != null) FilledImageUtility.SetAmount(progressFill, p.Progress);
            if (progressText != null) progressText.text = $"부활 중... {p.Progress * 100f:0}%";
        }

        private void HideBar()
        {
            if (cg != null) cg.alpha = 0f;
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

            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;

            var rt       = go.GetComponent<RectTransform>();
            rt.sizeDelta = canvasSize;

            cg       = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            barRoot  = go.transform;

            // 배경
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(go.transform, false);
            bgGO.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
            Stretch(bgGO);

            // 채우기 바
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(go.transform, false);
            progressFill              = fillGO.AddComponent<Image>();
            progressFill.color        = fillColor;
            var fillSprite = Resources.Load<Sprite>("Sprites/UI/WhiteSquare");
            if (fillSprite == null)
                Debug.LogWarning($"[{nameof(WorldReviveProgressUI)}] Sprites/UI/WhiteSquare 를 찾을 수 없습니다. 진행 바가 비어 보일 수 있습니다.", this);
            progressFill.sprite       = fillSprite;
            progressFill.type         = UnityEngine.UI.Image.Type.Filled;
            progressFill.fillMethod   = UnityEngine.UI.Image.FillMethod.Horizontal;
            progressFill.fillOrigin   = 0;
            progressFill.fillClockwise = true;
            progressFill.fillAmount   = 0f;
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
            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
