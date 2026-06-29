using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vamsurlike.Network;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject      clientPanel;
        [SerializeField] private Button          joinButton;
        [SerializeField] private Button          startGameButton;
        [SerializeField] private TMP_InputField  ipOrCodeInput;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI localIPText;
        [Header("Connection")]
        [SerializeField] private ushort defaultServerPort        = 7777;
        [SerializeField, Min(0.5f)] private float connectionTimeoutSeconds = 5f;
        [SerializeField, Min(50)]   private int   connectionPollIntervalMs = 200;
        [SerializeField] private string routeProbeHost  = "8.8.8.8";
        [SerializeField] private int    routeProbePort  = 65530;
        [SerializeField] private string fallbackLocalIp = "127.0.0.1";
        [Header("Labels")]
        [SerializeField] private string localIpFormat           = "내 IP: {0}";
        [SerializeField] private string emptyAddressMessage     = "서버 IP 또는 호스트명을 입력하세요.";
        [SerializeField] private string invalidAddressMessage   = "주소 형식이 올바르지 않습니다. 예: 192.168.0.10:7777";
        [SerializeField] private string connectingFormat        = "접속 중: {0}:{1}";
        [SerializeField] private string clientStartFailedMessage = "클라이언트 시작 실패.";
        [SerializeField] private string connectionFailedMessage = "접속 실패: 서버를 찾을 수 없습니다.";
        [SerializeField] private string lobbyHostStatusFormat   = "방장입니다 - 플레이어 {0}명, 게임 시작 가능";
        [SerializeField] private string lobbyClientStatusFormat = "플레이어 {0}명 접속 중 - 방장 시작 대기";
        [SerializeField] private string startRequestMessage     = "게임 시작 요청 중...";

        private LobbyViewModel viewModel;

        private void Awake()
        {
            if (IsServerMode())
            {
                if (clientPanel != null) clientPanel.SetActive(false);
                if (statusText  != null) statusText.gameObject.SetActive(false);
                if (localIPText != null) localIPText.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            viewModel = new LobbyViewModel(new LobbyViewModelConfig
            {
                DefaultServerPort        = defaultServerPort,
                ConnectionTimeoutSeconds = connectionTimeoutSeconds,
                ConnectionPollIntervalMs = connectionPollIntervalMs,
                RouteProbeHost           = routeProbeHost,
                RouteProbePort           = routeProbePort,
                FallbackLocalIp          = fallbackLocalIp,
                EmptyAddressMessage      = emptyAddressMessage,
                InvalidAddressMessage    = invalidAddressMessage,
                ConnectingFormat         = connectingFormat,
                ClientStartFailed        = clientStartFailedMessage,
                ConnectionFailed         = connectionFailedMessage,
                HostStatusFormat         = lobbyHostStatusFormat,
                ClientStatusFormat       = lobbyClientStatusFormat,
                StartRequestMessage      = startRequestMessage,
            });

            if (joinButton      != null) joinButton.onClick.AddListener(() => _ = viewModel.ConnectAsync(ipOrCodeInput != null ? ipOrCodeInput.text.Trim() : ""));
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(viewModel.StartGame);
                startGameButton.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (localIPText != null && viewModel != null)
                localIPText.text = string.Format(localIpFormat, viewModel.GetLocalIp());
        }

        private void OnEnable()
        {
            if (viewModel == null) return;
            viewModel.OnLobbyChanged += RefreshLobbyControls;
            viewModel.OnStatus       += SetStatus;
            viewModel.Bind();
        }

        private void OnDisable()
        {
            if (viewModel == null) return;
            viewModel.OnLobbyChanged -= RefreshLobbyControls;
            viewModel.OnStatus       -= SetStatus;
            viewModel.Dispose();
        }

        private void RefreshLobbyControls(bool isConnected, bool isHost, int _)
        {
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(isConnected);
                startGameButton.interactable = isHost;
            }
            if (joinButton     != null) joinButton.gameObject.SetActive(!isConnected);
            if (ipOrCodeInput  != null) ipOrCodeInput.gameObject.SetActive(!isConnected);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.Log($"[{nameof(MainMenuUI)}] {message}");
        }

        private static bool IsServerMode()
        {
            if (NetworkBootstrapper.IsServerBuild) return true;
#if UNITY_EDITOR
            foreach (string tag in Unity.Multiplayer.PlayMode.CurrentPlayer.Tags)
                if (string.Equals(tag, "Server", System.StringComparison.OrdinalIgnoreCase)) return true;
#endif
            return false;
        }
    }
}
