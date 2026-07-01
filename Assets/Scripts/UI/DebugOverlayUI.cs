using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.UI
{
    // Bootstrap Canvas에 배치. DontDestroyOnLoad.
    // DEVELOPMENT_BUILD 또는 UNITY_EDITOR 에서만 표시 — 릴리즈 빌드에선 자동으로 비활성화.
    //
    // FPS : 지수 이동 평균 (프레임마다 튀지 않도록)
    // Ping: NetworkTransport.GetCurrentRtt — 클라이언트가 서버에 연결됐을 때만 표시
    public class DebugOverlayUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private int refreshEveryFrames = 10;

        private float smoothFps;
        private int   frameCounter;

        private void Awake()
        {
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            gameObject.SetActive(false);
#endif
        }

        private void Update()
        {
            smoothFps = Mathf.Lerp(smoothFps, 1f / Time.unscaledDeltaTime, 0.1f);

            if (++frameCounter < refreshEveryFrames) return;
            frameCounter = 0;
            Refresh();
        }

        private void Refresh()
        {
            if (text == null) return;

            long pingMs = GetPingMs();
            text.text = pingMs >= 0
                ? $"FPS {smoothFps:F0}  Ping {pingMs}ms"
                : $"FPS {smoothFps:F0}";
        }

        // 클라이언트가 서버에 연결 중일 때만 RTT 반환. 미연결 시 -1.
        private static long GetPingMs()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient || !nm.IsConnectedClient) return -1;

            ulong rtt = nm.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
            return (long)rtt;
        }
    }
}
