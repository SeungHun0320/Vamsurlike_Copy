using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Vamsurlike.Core;

namespace Vamsurlike.Network
{
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public class GameNetworkManager : MonoBehaviour
    {
        public const ulong NoLobbyHost = LobbyHostService.NoLobbyHost;

        // C# 기본 매개변수 값은 컴파일 타임 상수여야 하므로 리터럴을 유지한다.
        // 실제 런타임 기본값은 NetworkConfigSO/SceneConfigSO(있는 경우)를 우선 사용 — 아래 Awake() 참고.
        private const string DefaultClientIp = "127.0.0.1";
        private const string DefaultServerIp = "0.0.0.0";
        private const ushort DefaultPort = 7777;
        private const string DefaultLobbySceneName = "MainMenu";
        private const string DefaultStageSceneName = "Stage_01";

        [SerializeField] private string lobbySceneName = DefaultLobbySceneName;
        [SerializeField] private string stageSceneName = DefaultStageSceneName;

        private NetworkManager networkManager;
        private INetworkSessionService sessionService;
        private ILobbyHostService lobbyHostService;
        private IGameStartService gameStartService;

        public static GameNetworkManager Instance { get; private set; }

        public event Action<ulong>  OnClientConnected;
        public event Action<ulong>  OnClientDisconnected;
        public event Action<ulong>  OnLobbyHostChanged;
        // 예기치 않은 연결 종료 시 발생. reason = 빈 문자열이면 서버가 이유를 제공하지 않음.
        public event Action<string> OnUnexpectedDisconnect;

        private bool isIntentionalDisconnect;

        public string CurrentIp => sessionService?.CurrentIp ?? ResolvedClientIp;
        public ushort CurrentPort => sessionService?.CurrentPort ?? ResolvedPort;

        private static string ResolvedClientIp => NetworkConfigSO.Instance != null ? NetworkConfigSO.Instance.defaultClientIp : DefaultClientIp;
        private static string ResolvedServerIp => NetworkConfigSO.Instance != null ? NetworkConfigSO.Instance.defaultServerIp : DefaultServerIp;
        private static ushort ResolvedPort     => NetworkConfigSO.Instance != null ? NetworkConfigSO.Instance.defaultPort     : DefaultPort;
        public ulong LobbyHostClientId => lobbyHostService?.LobbyHostClientId ?? NoLobbyHost;
        public bool IsLocalLobbyHost => lobbyHostService?.IsLocalLobbyHost ?? false;
        public int ConnectedPlayerCount => networkManager?.ConnectedClients?.Count ?? 0;
        public bool IsClientConnected => networkManager != null && networkManager.IsConnectedClient;
        public bool IsAvailableToStart => networkManager != null && !networkManager.IsListening;
        public bool IsListening => networkManager != null && networkManager.IsListening;
        public bool IsServer => networkManager != null && networkManager.IsServer;
        public bool IsHost => networkManager != null && networkManager.IsHost;
        public bool IsClientOnly => networkManager != null && networkManager.IsClient && !networkManager.IsServer;
        public string LastDisconnectReason => networkManager != null ? networkManager.DisconnectReason : string.Empty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            Instance = this;
            networkManager = GetComponent<NetworkManager>();
            UnityTransport transport = GetComponent<UnityTransport>();
            if (networkManager == null || transport == null)
            {
                Debug.LogError($"[{nameof(GameNetworkManager)}] NetworkManager 또는 UnityTransport가 없습니다.", this);
                enabled = false;
                return;
            }

#if UNITY_EDITOR
            // MPM 가상 플레이어가 프리팹 변경 직후 오래된 NetworkConfig 캐시로 튕기는 것을 막는다.
            networkManager.NetworkConfig.ForceSamePrefabs = false;
#endif

            ValidateSceneNames();
            sessionService = new NetworkSessionService(networkManager, transport, ResolvedClientIp, ResolvedPort);
            lobbyHostService = new LobbyHostService(networkManager);
            gameStartService = new GameStartService(
                networkManager,
                lobbyHostService,
                lobbySceneName,
                stageSceneName);
        }

        private void OnEnable()
        {
            if (networkManager == null || sessionService == null || lobbyHostService == null) return;

            sessionService.ConfigureConnectionApproval();
            networkManager.OnClientConnectedCallback  += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            networkManager.OnClientStopped            += HandleClientStopped;
            networkManager.OnServerStarted            += RegisterMessagingHandlers;
            networkManager.OnClientStarted            += RegisterMessagingHandlers;
            lobbyHostService.LobbyHostChanged         += HandleLobbyHostChanged;
        }

        private void OnDisable()
        {
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback  -= HandleClientConnected;
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                networkManager.OnClientStopped            -= HandleClientStopped;
                networkManager.OnServerStarted            -= RegisterMessagingHandlers;
                networkManager.OnClientStarted            -= RegisterMessagingHandlers;
            }

            if (lobbyHostService != null)
                lobbyHostService.LobbyHostChanged -= HandleLobbyHostChanged;

            UnregisterMessagingHandlers();
        }

        private void OnDestroy()
        {
            gameStartService?.Dispose();
            lobbyHostService?.Dispose();
            sessionService?.Dispose();

            if (Instance == this) Instance = null;
        }

        [Obsolete("호스트 모드 미사용. 전용 서버 아키텍처로 전환됨 - StartAsServer() + StartAsClient() 사용.")]
        public bool StartAsHost(string ip = DefaultClientIp, ushort port = DefaultPort)
        {
            Debug.LogError($"[{nameof(GameNetworkManager)}] StartAsHost는 더 이상 사용하지 않습니다.");
            return false;
        }

        [Obsolete("호스트 모드 미사용. 전용 서버 아키텍처로 전환됨 - StartAsServer() + StartAsRelayClient() 사용.")]
        public bool StartAsRelayHost()
        {
            Debug.LogError($"[{nameof(GameNetworkManager)}] StartAsRelayHost는 더 이상 사용하지 않습니다.");
            return false;
        }

        public bool StartAsClient(string ip = DefaultClientIp, ushort port = DefaultPort, string nickname = "")
        {
            bool started = sessionService?.StartClient(ip, port, nickname) ?? false;
            if (started) RegisterMessagingHandlers();
            return started;
        }

        public bool StartAsRelayClient()
        {
            bool started = sessionService?.StartRelayClient() ?? false;
            if (started) RegisterMessagingHandlers();
            return started;
        }

        public bool StartAsServer(string ip = DefaultServerIp, ushort port = DefaultPort)
        {
            bool started = sessionService?.StartServer(ip, port) ?? false;
            if (started) RegisterMessagingHandlers();
            return started;
        }

        public void Disconnect()
        {
            // Shutdown() 전에 호출해야 CustomMessagingManager가 유효한 상태에서 플래그 초기화됨.
            // Shutdown() 이후엔 CustomMessagingManager가 null이 되어 isMessageHandlerRegistered가
            // 리셋되지 않고, 다음 연결 시 RegisterMessageHandler()가 스킵되어 방장 정보를 못 받음.
            isIntentionalDisconnect = true;
            UnregisterMessagingHandlers();
            sessionService?.Disconnect();
            lobbyHostService?.Clear();
        }

        public bool RequestStartGame()
        {
            return gameStartService?.RequestStartGame() ?? false;
        }

        public bool RequestReturnToLobby()
        {
            return gameStartService?.RequestReturnToLobby() ?? false;
        }

        private void RegisterMessagingHandlers()
        {
            lobbyHostService?.RegisterMessageHandler();
            gameStartService?.RegisterMessageHandler();
        }

        private void UnregisterMessagingHandlers()
        {
            gameStartService?.UnregisterMessageHandler();
            lobbyHostService?.UnregisterMessageHandler();
        }

        private void HandleClientConnected(ulong clientId)
        {
            lobbyHostService?.HandleClientConnected(clientId);
            OnClientConnected?.Invoke(clientId);
            Debug.Log($"[{nameof(GameNetworkManager)}] 플레이어 {ConnectedPlayerCount}명 접속 (clientId: {clientId})");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            lobbyHostService?.HandleClientDisconnected(clientId);
            OnClientDisconnected?.Invoke(clientId);
            Debug.Log($"[{nameof(GameNetworkManager)}] clientId {clientId} 종료. 현재 {ConnectedPlayerCount}명");
        }

        private void HandleClientStopped(bool _)
        {
            if (isIntentionalDisconnect)
            {
                isIntentionalDisconnect = false;
                return;
            }

            string reason = networkManager != null && !string.IsNullOrEmpty(networkManager.DisconnectReason)
                ? networkManager.DisconnectReason
                : string.Empty;
            OnUnexpectedDisconnect?.Invoke(reason);
        }

        private void HandleLobbyHostChanged(ulong clientId)
        {
            OnLobbyHostChanged?.Invoke(clientId);
        }

        private void ValidateSceneNames()
        {
            if (string.IsNullOrWhiteSpace(lobbySceneName))
            {
                Debug.LogWarning($"[{nameof(GameNetworkManager)}] 로비 씬 이름이 비어 있어 기본값을 사용합니다.", this);
                lobbySceneName = SceneConfigSO.Instance != null ? SceneConfigSO.Instance.mainMenuSceneName : DefaultLobbySceneName;
            }

            if (string.IsNullOrWhiteSpace(stageSceneName))
            {
                Debug.LogWarning($"[{nameof(GameNetworkManager)}] 스테이지 씬 이름이 비어 있어 기본값을 사용합니다.", this);
                stageSceneName = SceneConfigSO.Instance != null ? SceneConfigSO.Instance.stageSceneName : DefaultStageSceneName;
            }
        }
    }
}
