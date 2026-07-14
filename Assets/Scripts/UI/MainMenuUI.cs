using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vamsurlike.Network;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        private const string PrefKeyLastIp = "LastServerIp";
        private const string PrefKeyLoginUsername = "LoginUsername"; // LoginUI와 동일한 PlayerPrefs 키 — 로그인 아이디가 곧 닉네임.

        [SerializeField] private GameObject      connectPanel;
        [SerializeField] private LoginUI         loginUI;
        [SerializeField] private LobbyUI         lobbyUI;
        [SerializeField] private TMP_InputField  serverIpInput;
        [SerializeField] private Button          joinButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [Header("Connection")]
        [SerializeField] private ushort defaultServerPort         = 7777;
        [SerializeField, Min(0.5f)] private float connectionTimeoutSeconds = 5f;
        [SerializeField, Min(50)]   private int   connectionPollIntervalMs = 200;
        [SerializeField] private string routeProbeHost  = "8.8.8.8";
        [SerializeField] private int    routeProbePort  = 65530;
        [SerializeField] private string fallbackLocalIp = "127.0.0.1";
        [Header("Labels")]
        [SerializeField] private string emptyAddressMessage      = "서버 IP를 입력하세요. 예: 192.168.0.10:7777";
        [SerializeField] private string invalidAddressMessage    = "주소 형식이 올바르지 않습니다. 예: 192.168.0.10:7777";
        [SerializeField] private string connectingFormat         = "접속 중: {0}:{1}";
        [SerializeField] private string clientStartFailedMessage = "클라이언트 시작 실패.";
        [SerializeField] private string connectionFailedMessage  = "접속 실패: 서버를 찾을 수 없습니다.";
        [SerializeField] private string lobbyHostStatusFormat    = "방장입니다 — 플레이어 {0}명";
        [SerializeField] private string lobbyClientStatusFormat  = "플레이어 {0}명 접속 중 — 방장 시작 대기";
        [SerializeField] private string startRequestMessage      = "게임 시작 요청 중...";

        private LobbyViewModel viewModel;
        private bool isLoggedIn;
        private string loginNickname = "";

        private void Awake()
        {
            if (IsServerMode())
            {
                if (connectPanel != null) connectPanel.SetActive(false);
                if (loginUI      != null) loginUI.gameObject.SetActive(false);
                if (lobbyUI      != null) lobbyUI.gameObject.SetActive(false);
                if (statusText   != null) statusText.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            // 로그인(Username/Password) 완료 전까지는 접속 패널을 숨긴다.
            isLoggedIn = NetworkBootstrapper.IsSignedIn;
            if (connectPanel != null) connectPanel.SetActive(isLoggedIn);
            if (loginUI      != null) loginUI.gameObject.SetActive(!isLoggedIn);

            // 공유 NetworkConfig가 있으면 그쪽을 우선시켜, GameNetworkManager 등 다른 곳과
            // 기본 IP/포트가 어긋나지 않게 한다. 없으면 위 [SerializeField] 기본값을 그대로 사용.
            if (NetworkConfigSO.Instance != null)
            {
                defaultServerPort = NetworkConfigSO.Instance.defaultPort;
                fallbackLocalIp   = NetworkConfigSO.Instance.defaultClientIp;
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

            RestorePrefs();

            if (joinButton != null)
                joinButton.onClick.AddListener(OnJoinClicked);

            if (lobbyUI != null)
                lobbyUI.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (loginUI != null) loginUI.LoggedIn += HandleLoggedIn;
            NetworkBootstrapper.OnUgsReady += HandleUgsReady;
            // UGS 초기화가 Awake() 시점 이후에 끝나며 캐시된 세션으로 이미 로그인된 상태가 될 수 있어 재확인.
            if (!isLoggedIn && NetworkBootstrapper.IsSignedIn) HandleLoggedIn(PlayerPrefs.GetString(PrefKeyLoginUsername, ""));

            if (viewModel == null) return;
            viewModel.OnLobbyChanged += RefreshLobbyControls;
            viewModel.OnStatus       += SetStatus;
            viewModel.Bind();
            // 이미 연결된 채로 씬이 로드된 경우(로비 복귀) 현재 상태 즉시 반영
            viewModel.Refresh();
        }

        private void OnDisable()
        {
            if (loginUI != null) loginUI.LoggedIn -= HandleLoggedIn;
            NetworkBootstrapper.OnUgsReady -= HandleUgsReady;

            if (viewModel == null) return;
            viewModel.OnLobbyChanged -= RefreshLobbyControls;
            viewModel.OnStatus       -= SetStatus;
            viewModel.Unbind();
        }

        private void HandleLoggedIn(string username)
        {
            isLoggedIn = true;
            loginNickname = username ?? "";
            if (loginUI != null) loginUI.gameObject.SetActive(false);
            RefreshLobbyControls(false, false, 0);
        }

        private void HandleUgsReady()
        {
            if (!isLoggedIn && NetworkBootstrapper.IsSignedIn)
                HandleLoggedIn(PlayerPrefs.GetString(PrefKeyLoginUsername, ""));
        }

        private void OnDestroy()
        {
            viewModel?.Dispose();
        }

        private void OnJoinClicked()
        {
            string ip = serverIpInput != null ? serverIpInput.text.Trim() : "";
            if (!string.IsNullOrEmpty(ip))
                PlayerPrefs.SetString(PrefKeyLastIp, ip);
            _ = viewModel.ConnectAsync(ip, loginNickname);
        }

        private void RefreshLobbyControls(bool isConnected, bool isHost, int _)
        {
            if (connectPanel != null) connectPanel.SetActive(!isConnected && isLoggedIn);
            if (lobbyUI      != null) lobbyUI.gameObject.SetActive(isConnected);
            if (joinButton    != null) joinButton.gameObject.SetActive(!isConnected);
            if (serverIpInput != null) serverIpInput.interactable = !isConnected;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.Log($"[{nameof(MainMenuUI)}] {message}");
        }

        private void RestorePrefs()
        {
            if (serverIpInput != null) serverIpInput.text = PlayerPrefs.GetString(PrefKeyLastIp, "");
        }

        private static bool IsServerMode() => NetworkBootstrapper.IsServerMode();
    }
}
