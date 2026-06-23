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
                localIPText.text = $"내 IP: {GetLocalIP()}";
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

        private static string GetLocalIP()
        {
            // UDP 소켓으로 실제 라우팅에 사용되는 LAN IP를 가져온다 (패킷 전송 없음)
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 65530);
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
            }
            catch { }
            return "127.0.0.1";
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
                SetStatus("서버 IP 또는 호스트명을 입력하세요.");
                return;
            }

            isBusy = true;
            try
            {
                if (!TryParseEndpoint(input, out string ip, out ushort port))
                {
                    SetStatus("주소 형식이 올바르지 않습니다. 예: 192.168.0.10:7777");
                    return;
                }

                SetStatus($"접속 중: {ip}:{port}");
                var gnm = GameNetworkManager.Instance;
                if (gnm == null || !gnm.StartAsClient(ip, port))
                {
                    SetStatus("클라이언트 시작 실패.");
                    return;
                }
                await WaitForConnectionAsync(timeoutSeconds: 5f);
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
                await Task.Delay(200);
                elapsed += 0.2f;
            }
            SetStatus("접속 실패: 서버를 찾을 수 없습니다.");
            GameNetworkManager.Instance?.Disconnect();
            return false;
        }

        private static bool TryParseEndpoint(string input, out string ip, out ushort port)
        {
            const ushort defaultPort = 7777;

            ip = input.Trim();
            port = defaultPort;
            if (string.IsNullOrWhiteSpace(ip)) return false;

            if (ushort.TryParse(input, out ushort portOnly))
            {
                ip = "127.0.0.1";
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
                ? $"방장입니다 — 플레이어 {count}명, 게임 시작 가능"
                : $"플레이어 {count}명 접속 중 — 방장 시작 대기");
        }

        private void OnStartGameClicked()
        {
            if (GameNetworkManager.Instance?.RequestStartGame() == true)
                SetStatus("게임 시작 요청 중...");
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
            Debug.Log($"[{nameof(MainMenuUI)}] {message}");
        }
    }
}
