using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Vamsurlike.Network;

namespace Vamsurlike.UI.ViewModels
{
    public sealed class LobbyViewModelConfig
    {
        public ushort DefaultServerPort        = 7777;
        public float  ConnectionTimeoutSeconds = 5f;
        public int    ConnectionPollIntervalMs = 200;
        public string RouteProbeHost           = "8.8.8.8";
        public int    RouteProbePort           = 65530;
        public string FallbackLocalIp          = "127.0.0.1";

        public string EmptyAddressMessage    = "서버 IP 또는 호스트명을 입력하세요.";
        public string InvalidAddressMessage  = "주소 형식이 올바르지 않습니다. 예: 192.168.0.10:7777";
        public string ConnectingFormat       = "접속 중: {0}:{1}";
        public string ClientStartFailed      = "클라이언트 시작 실패.";
        public string ConnectionFailed       = "접속 실패: 서버를 찾을 수 없습니다.";
        public string HostStatusFormat       = "방장입니다 - 플레이어 {0}명, 게임 시작 가능";
        public string ClientStatusFormat     = "플레이어 {0}명 접속 중 - 방장 시작 대기";
        public string StartRequestMessage    = "게임 시작 요청 중...";
    }

    // GameNetworkManager 이벤트 구독 + 접속 커맨드를 담당한다.
    public sealed class LobbyViewModel : ViewModelBase
    {
        // View가 구독하는 이벤트
        public event Action<bool, bool, int> OnLobbyChanged; // isConnected, isHost, playerCount
        public event Action<string>          OnStatus;       // 상태 메시지 갱신

        private readonly LobbyViewModelConfig cfg;
        private bool isBusy;

        public LobbyViewModel(LobbyViewModelConfig config)
        {
            cfg = config ?? new LobbyViewModelConfig();
        }

        protected override void Subscribe()
        {
            if (GameNetworkManager.Instance == null) return;
            GameNetworkManager.Instance.OnClientConnected    += HandlePlayerCount;
            GameNetworkManager.Instance.OnClientDisconnected += HandlePlayerCount;
            GameNetworkManager.Instance.OnLobbyHostChanged   += HandleLobbyHost;
        }

        protected override void Unsubscribe()
        {
            if (GameNetworkManager.Instance == null) return;
            GameNetworkManager.Instance.OnClientConnected    -= HandlePlayerCount;
            GameNetworkManager.Instance.OnClientDisconnected -= HandlePlayerCount;
            GameNetworkManager.Instance.OnLobbyHostChanged   -= HandleLobbyHost;
        }

        // View 초기화 시 LAN IP 표시용
        public string GetLocalIp()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(cfg.RouteProbeHost, cfg.RouteProbePort);
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? cfg.FallbackLocalIp;
            }
            catch { }
            return cfg.FallbackLocalIp;
        }

        // 커맨드: IP 또는 호스트명으로 전용 서버에 참여
        public async Task ConnectAsync(string input)
        {
            if (isBusy) return;

            if (string.IsNullOrEmpty(input))
            {
                OnStatus?.Invoke(cfg.EmptyAddressMessage);
                return;
            }

            if (!TryParseEndpoint(input, out string ip, out ushort port))
            {
                OnStatus?.Invoke(cfg.InvalidAddressMessage);
                return;
            }

            isBusy = true;
            try
            {
                OnStatus?.Invoke(string.Format(cfg.ConnectingFormat, ip, port));
                var gnm = GameNetworkManager.Instance;
                if (gnm == null || !gnm.StartAsClient(ip, port))
                {
                    OnStatus?.Invoke(cfg.ClientStartFailed);
                    return;
                }
                await WaitForConnectionAsync(cfg.ConnectionTimeoutSeconds);
            }
            finally
            {
                isBusy = false;
            }
        }

        // 커맨드: 게임 시작 (방장 전용)
        public void StartGame()
        {
            if (GameNetworkManager.Instance?.RequestStartGame() == true)
                OnStatus?.Invoke(cfg.StartRequestMessage);
        }

        private void HandlePlayerCount(ulong _) => PublishLobbyState();
        private void HandleLobbyHost(ulong _)   => PublishLobbyState();

        private void PublishLobbyState()
        {
            var gnm = GameNetworkManager.Instance;
            bool connected = gnm != null && gnm.IsClientConnected;
            bool isHost    = connected && gnm.IsLocalLobbyHost;
            int  count     = connected ? gnm.ConnectedPlayerCount : 0;
            OnLobbyChanged?.Invoke(connected, isHost, count);

            if (connected)
            {
                string msg = isHost
                    ? string.Format(cfg.HostStatusFormat, count)
                    : string.Format(cfg.ClientStatusFormat, count);
                OnStatus?.Invoke(msg);
            }
        }

        private bool TryParseEndpoint(string input, out string ip, out ushort port)
        {
            ip   = input.Trim();
            port = cfg.DefaultServerPort;
            if (string.IsNullOrWhiteSpace(ip)) return false;

            if (ushort.TryParse(input, out ushort portOnly))
            {
                ip   = cfg.FallbackLocalIp;
                port = portOnly;
                return true;
            }

            int sep = input.LastIndexOf(':');
            if (sep <= 0 || sep >= input.Length - 1)
                return !input.Contains(":");

            string portText = input[(sep + 1)..];
            if (!ushort.TryParse(portText, out ushort parsedPort) || parsedPort == 0)
                return false;

            ip   = input[..sep].Trim();
            port = parsedPort;
            return !string.IsNullOrWhiteSpace(ip);
        }

        private async Task<bool> WaitForConnectionAsync(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.IsClientConnected)
                {
                    PublishLobbyState();
                    return true;
                }
                await Task.Delay(cfg.ConnectionPollIntervalMs);
                elapsed += cfg.ConnectionPollIntervalMs / 1000f;
            }
            OnStatus?.Invoke(cfg.ConnectionFailed);
            GameNetworkManager.Instance?.Disconnect();
            return false;
        }
    }
}
