using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vamsurlike.Network;

namespace Vamsurlike.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject clientPanel;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private TMP_InputField ipOrCodeInput;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI localIPText;
        [Header("Connection")]
        [SerializeField] private ushort defaultServerPort = 7777;
        [SerializeField, Min(0.5f)] private float connectionTimeoutSeconds = 5f;
        [SerializeField, Min(50)] private int connectionPollIntervalMs = 200;
        [SerializeField] private string routeProbeHost = "8.8.8.8";
        [SerializeField] private int routeProbePort = 65530;
        [SerializeField] private string fallbackLocalIp = "127.0.0.1";
        [Header("Labels")]
        [SerializeField] private string localIpFormat = "내 IP: {0}";
        [SerializeField] private string emptyAddressMessage = "서버 IP 또는 호스트명을 입력하세요.";
        [SerializeField] private string invalidAddressMessage = "주소 형식이 올바르지 않습니다. 예: 192.168.0.10:7777";
        [SerializeField] private string connectingFormat = "접속 중: {0}:{1}";
        [SerializeField] private string clientStartFailedMessage = "클라이언트 시작 실패.";
        [SerializeField] private string connectionFailedMessage = "접속 실패: 서버를 찾을 수 없습니다.";
        [SerializeField] private string lobbyHostStatusFormat = "방장입니다 - 플레이어 {0}명, 게임 시작 가능";
        [SerializeField] private string lobbyClientStatusFormat = "플레이어 {0}명 접속 중 - 방장 시작 대기";
        [SerializeField] private string startRequestMessage = "게임 시작 요청 중...";

        private bool isBusy;

        private void Awake()
        {
            // 서버에서는 클라이언트 UI만 숨김 (Canvas 전체 비활성화 금지 — ServerAdminPanel이 같은 Canvas에 있음)
            if (IsServerMode())
            {
                if (clientPanel != null) clientPanel.SetActive(false);
                if (statusText != null) statusText.gameObject.SetActive(false);
                if (localIPText != null) localIPText.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            if (joinButton != null)
                joinButton.onClick.AddListener(() => _ = OnJoinClickedAsync());
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
                startGameButton.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (localIPText != null)
                localIPText.text = string.Format(localIpFormat, GetLocalIP());
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

        private string GetLocalIP()
        {
            // UDP 소켓으로 실제 라우팅에 사용되는 LAN IP를 가져온다 (패킷 전송 없음)
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(routeProbeHost, routeProbePort);
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? fallbackLocalIp;
            }
            catch { }
            return fallbackLocalIp;
        }

        private void OnEnable()
        {
            if (GameNetworkManager.Instance == null) return;
            GameNetworkManager.Instance.OnClientConnected    += HandlePlayerCountChanged;
            GameNetworkManager.Instance.OnClientDisconnected += HandlePlayerCountChanged;
            GameNetworkManager.Instance.OnLobbyHostChanged    += HandleLobbyHostChanged;
        }

        private void OnDisable()
        {
            if (GameNetworkManager.Instance == null) return;
            GameNetworkManager.Instance.OnClientConnected    -= HandlePlayerCountChanged;
            GameNetworkManager.Instance.OnClientDisconnected -= HandlePlayerCountChanged;
            GameNetworkManager.Instance.OnLobbyHostChanged    -= HandleLobbyHostChanged;
        }

        // IP 또는 호스트명으로 전용 서버에 참여
        private async Task OnJoinClickedAsync()
        {
            if (isBusy) return;

            string input = ipOrCodeInput != null ? ipOrCodeInput.text.Trim() : "";

            if (string.IsNullOrEmpty(input))
            {
                SetStatus(emptyAddressMessage);
                return;
            }

            isBusy = true;
            try
            {
                if (!TryParseEndpoint(input, out string ip, out ushort port))
                {
                    SetStatus(invalidAddressMessage);
                    return;
                }

                SetStatus(string.Format(connectingFormat, ip, port));
                var gnm = GameNetworkManager.Instance;
                if (gnm == null || !gnm.StartAsClient(ip, port))
                {
                    SetStatus(clientStartFailedMessage);
                    return;
                }
                await WaitForConnectionAsync(connectionTimeoutSeconds);
            }
            finally
            {
                isBusy = false;
            }
        }

        private async Task<bool> WaitForConnectionAsync(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.IsClientConnected)
                {
                    RefreshLobbyControls();
                    return true;
                }
                await Task.Delay(connectionPollIntervalMs);
                elapsed += connectionPollIntervalMs / 1000f;
            }
            SetStatus(connectionFailedMessage);
            GameNetworkManager.Instance?.Disconnect();
            return false;
        }

        private bool TryParseEndpoint(string input, out string ip, out ushort port)
        {
            ip = input.Trim();
            port = defaultServerPort;
            if (string.IsNullOrWhiteSpace(ip)) return false;

            if (ushort.TryParse(input, out ushort portOnly))
            {
                ip = fallbackLocalIp;
                port = portOnly;
                return true;
            }

            int separatorIndex = input.LastIndexOf(':');
            if (separatorIndex <= 0 || separatorIndex >= input.Length - 1)
                return !input.Contains(":");

            string portText = input[(separatorIndex + 1)..];
            if (!ushort.TryParse(portText, out ushort parsedPort) || parsedPort == 0)
                return false;

            ip = input[..separatorIndex].Trim();
            port = parsedPort;
            return !string.IsNullOrWhiteSpace(ip);
        }

        private void HandlePlayerCountChanged(ulong _)
        {
            RefreshLobbyControls();
        }

        private void HandleLobbyHostChanged(ulong _)
        {
            RefreshLobbyControls();
        }

        private void RefreshLobbyControls()
        {
            var gnm = GameNetworkManager.Instance;
            bool connected = gnm != null && gnm.IsClientConnected;
            bool isHost = connected && gnm.IsLocalLobbyHost;

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(connected);
                startGameButton.interactable = isHost;
            }
            if (joinButton != null)
                joinButton.gameObject.SetActive(!connected);
            if (ipOrCodeInput != null)
                ipOrCodeInput.gameObject.SetActive(!connected);

            if (!connected) return;

            int count = gnm.ConnectedPlayerCount;
            SetStatus(isHost
                ? string.Format(lobbyHostStatusFormat, count)
                : string.Format(lobbyClientStatusFormat, count));
        }

        private void OnStartGameClicked()
        {
            if (GameNetworkManager.Instance?.RequestStartGame() == true)
                SetStatus(startRequestMessage);
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
            Debug.Log($"[{nameof(MainMenuUI)}] {message}");
        }
    }
}
