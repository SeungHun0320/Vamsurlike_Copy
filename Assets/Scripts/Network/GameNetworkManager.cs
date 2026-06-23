using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Vamsurlike.Network
{
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public class GameNetworkManager : MonoBehaviour
    {
        public const ulong NoLobbyHost = LobbyHostService.NoLobbyHost;

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

        public event Action<ulong> OnClientConnected;
        public event Action<ulong> OnClientDisconnected;
        public event Action<ulong> OnLobbyHostChanged;

        public string CurrentIp => sessionService?.CurrentIp ?? DefaultClientIp;
        public ushort CurrentPort => sessionService?.CurrentPort ?? DefaultPort;
        public ulong LobbyHostClientId => lobbyHostService?.LobbyHostClientId ?? NoLobbyHost;
        public bool IsLocalLobbyHost => lobbyHostService?.IsLocalLobbyHost ?? false;
        public int ConnectedPlayerCount => networkManager?.ConnectedClients?.Count ?? 0;
        public bool IsClientConnected => networkManager != null && networkManager.IsConnectedClient;
        public bool IsAvailableToStart => networkManager != null && !networkManager.IsListening;
        public bool IsListening => networkManager != null && networkManager.IsListening;
        public bool IsServer => networkManager != null && networkManager.IsServer;
        public bool IsHost => networkManager != null && networkManager.IsHost;
        public bool IsClientOnly => networkManager != null && networkManager.IsClient && !networkManager.IsServer;

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

            ValidateSceneNames();
            sessionService = new NetworkSessionService(networkManager, transport, DefaultClientIp, DefaultPort);
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
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            networkManager.OnServerStarted += RegisterMessagingHandlers;
            networkManager.OnClientStarted += RegisterMessagingHandlers;
            lobbyHostService.LobbyHostChanged += HandleLobbyHostChanged;
        }

        private void OnDisable()
        {
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= HandleClientConnected;
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                networkManager.OnServerStarted -= RegisterMessagingHandlers;
                networkManager.OnClientStarted -= RegisterMessagingHandlers;
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

        public bool StartAsClient(string ip = DefaultClientIp, ushort port = DefaultPort)
        {
            bool started = sessionService?.StartClient(ip, port) ?? false;
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
            sessionService?.Disconnect();
            lobbyHostService?.Clear();
        }

        public bool RequestStartGame()
        {
            return gameStartService?.RequestStartGame() ?? false;
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

        private void HandleLobbyHostChanged(ulong clientId)
        {
            OnLobbyHostChanged?.Invoke(clientId);
        }

        private void ValidateSceneNames()
        {
            if (string.IsNullOrWhiteSpace(lobbySceneName))
            {
                Debug.LogWarning($"[{nameof(GameNetworkManager)}] 로비 씬 이름이 비어 있어 기본값을 사용합니다.", this);
                lobbySceneName = DefaultLobbySceneName;
            }

            if (string.IsNullOrWhiteSpace(stageSceneName))
            {
                Debug.LogWarning($"[{nameof(GameNetworkManager)}] 스테이지 씬 이름이 비어 있어 기본값을 사용합니다.", this);
                stageSceneName = DefaultStageSceneName;
            }
        }
    }
}
