using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vamsurlike.Network;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        private const string PrefKeyName   = "PlayerName";
        private const string PrefKeyLastIp = "LastServerIp";

        [SerializeField] private GameObject      connectPanel;
        [SerializeField] private LobbyUI         lobbyUI;
        [SerializeField] private TMP_InputField  nameInput;
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

        private void Awake()
        {
            if (IsServerMode())
            {
                if (connectPanel != null) connectPanel.SetActive(false);
                if (lobbyUI      != null) lobbyUI.gameObject.SetActive(false);
                if (statusText   != null) statusText.gameObject.SetActive(false);
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

            RestorePrefs();

            if (nameInput != null)
                nameInput.onEndEdit.AddListener(SaveName);

            if (joinButton != null)
                joinButton.onClick.AddListener(OnJoinClicked);

            if (lobbyUI != null)
                lobbyUI.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (viewModel == null) return;
            viewModel.OnLobbyChanged += RefreshLobbyControls;
            viewModel.OnStatus       += SetStatus;
            viewModel.Bind();
            // 이미 연결된 채로 씬이 로드된 경우(로비 복귀) 현재 상태 즉시 반영
            viewModel.Refresh();
        }

        private void OnDisable()
        {
            if (viewModel == null) return;
            viewModel.OnLobbyChanged -= RefreshLobbyControls;
            viewModel.OnStatus       -= SetStatus;
            viewModel.Unbind();
        }

        private void OnDestroy()
        {
            viewModel?.Dispose();
        }

        private void OnJoinClicked()
        {
            string name = nameInput != null ? nameInput.text.Trim() : "";
            SaveName(name);
            string ip = serverIpInput != null ? serverIpInput.text.Trim() : "";
            if (!string.IsNullOrEmpty(ip))
                PlayerPrefs.SetString(PrefKeyLastIp, ip);
            _ = viewModel.ConnectAsync(ip, name);
        }

        private void RefreshLobbyControls(bool isConnected, bool isHost, int _)
        {
            if (connectPanel != null) connectPanel.SetActive(!isConnected);
            if (lobbyUI      != null) lobbyUI.gameObject.SetActive(isConnected);
            if (joinButton    != null) joinButton.gameObject.SetActive(!isConnected);
            if (nameInput     != null) nameInput.interactable = !isConnected;
            if (serverIpInput != null) serverIpInput.interactable = !isConnected;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.Log($"[{nameof(MainMenuUI)}] {message}");
        }

        private void RestorePrefs()
        {
            if (nameInput     != null) nameInput.text     = PlayerPrefs.GetString(PrefKeyName, "");
            if (serverIpInput != null) serverIpInput.text = PlayerPrefs.GetString(PrefKeyLastIp, "");
        }

        private void SaveName(string value)
        {
            PlayerPrefs.SetString(PrefKeyName, value.Trim());
        }

        private static bool IsServerMode() => NetworkBootstrapper.IsServerMode();
    }
}
