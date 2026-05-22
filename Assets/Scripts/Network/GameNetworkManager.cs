using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Vamsurlike.Network
{
    [RequireComponent(typeof(UnityTransport))]
    public class GameNetworkManager : MonoBehaviour
    {
        public static GameNetworkManager Instance { get; private set; }

        public event Action<ulong> OnClientConnected;
        public event Action<ulong> OnClientDisconnected;

        public string CurrentIp { get; private set; } = "127.0.0.1";
        public ushort CurrentPort { get; private set; } = 7777;

        public int ConnectedPlayerCount =>
            NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClients?.Count ?? 0 : 0;

        public bool IsClientConnected =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;

        public bool IsAvailableToStart =>
            NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening;

        public bool IsListening =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        public bool IsServer =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        public bool IsHost =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        public bool IsClientOnly =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer;

        private UnityTransport transport;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transport = GetComponent<UnityTransport>();
        }

        private void Start()
        {
            if (NetworkManager.Singleton == null) return;
            ConfigureConnectionApproval();
            NetworkManager.Singleton.OnClientConnectedCallback    += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback   += HandleClientDisconnected;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientConnectedCallback    -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback   -= HandleClientDisconnected;
            if (NetworkManager.Singleton.ConnectionApprovalCallback == HandleConnectionApproval)
                NetworkManager.Singleton.ConnectionApprovalCallback = null;
        }

        public bool StartAsHost(string ip = "127.0.0.1", ushort port = 7777)
        {
            if (!CanStart("StartAsHost")) return false;
            ConfigureConnectionApproval();

            const int maxPortAttempts = 16;
            for (int i = 0; i < maxPortAttempts; i++)
            {
                ushort candidatePort = (ushort)(port + i);
                if (!CanBindUdp(ip, candidatePort))
                {
                    Debug.LogWarning($"[GameNetworkManager] {ip}:{candidatePort} 포트가 이미 사용 중입니다. 다음 포트를 시도합니다.");
                    continue;
                }

                if (!TrySetTransport(ip, candidatePort)) return false;

                bool ok = NetworkManager.Singleton.StartHost();
                if (ok)
                {
                    Debug.Log($"[GameNetworkManager] Host 시작 — {ip}:{candidatePort} (ok=True)");
                    return true;
                }

                Debug.LogWarning($"[GameNetworkManager] Host 시작 실패 — {ip}:{candidatePort}. 다음 포트를 시도합니다.");
                NetworkManager.Singleton.Shutdown();
            }

            Debug.LogError($"[GameNetworkManager] Host 시작 실패: {ip}:{port}부터 {maxPortAttempts}개 포트를 사용할 수 없습니다.");
            return false;
        }

        // Relay 호스트 — SDK가 transport를 이미 설정했으므로 SetConnectionData 호출 안 함
        public bool StartAsRelayHost()
        {
            if (!CanStart("StartAsRelayHost")) return false;
            ConfigureConnectionApproval();
            bool ok = NetworkManager.Singleton.StartHost();
            Debug.Log($"[GameNetworkManager] Relay Host 시작 (ok={ok})");
            return ok;
        }

        public bool StartAsClient(string ip = "127.0.0.1", ushort port = 7777)
        {
            if (!CanStart("StartAsClient")) return false;
            if (!TrySetTransport(ip, port)) return false;
            ConfigureConnectionApproval();
            bool ok = NetworkManager.Singleton.StartClient();
            Debug.Log($"[GameNetworkManager] Client 시작 — {ip}:{port} (ok={ok})");
            return ok;
        }

        // Relay 클라이언트 — SDK가 transport를 이미 설정했으므로 SetConnectionData 호출 안 함
        public bool StartAsRelayClient()
        {
            if (!CanStart("StartAsRelayClient")) return false;
            ConfigureConnectionApproval();
            bool ok = NetworkManager.Singleton.StartClient();
            Debug.Log($"[GameNetworkManager] Relay Client 시작 (ok={ok})");
            return ok;
        }

        public bool StartAsServer(string ip = "0.0.0.0", ushort port = 7777)
        {
            if (!CanStart("StartAsServer")) return false;
            if (!TrySetTransport(ip, port)) return false;
            ConfigureConnectionApproval();
            bool ok = NetworkManager.Singleton.StartServer();
            Debug.Log($"[GameNetworkManager] Server 시작 — {ip}:{port} (ok={ok})");
            return ok;
        }

        public void Disconnect()
        {
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.Shutdown();
            Debug.Log("[GameNetworkManager] 연결 종료.");
        }

        // Singleton null + IsListening 이중 가드
        private bool CanStart(string caller)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError($"[GameNetworkManager] {caller}: NetworkManager.Singleton이 null입니다.");
                return false;
            }
            if (NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning($"[GameNetworkManager] {caller}: 이미 실행 중 — 무시");
                return false;
            }
            return true;
        }

        // transport 설정 실패 시 false 반환 → 시작 중단
        private bool TrySetTransport(string ip, ushort port)
        {
            if (transport == null)
            {
                Debug.LogError($"[GameNetworkManager] UnityTransport를 찾을 수 없습니다.");
                return false;
            }
            transport.SetConnectionData(ip, port);
            CurrentIp = ip;
            CurrentPort = port;
            return true;
        }

        private bool CanBindUdp(string ip, ushort port)
        {
            try
            {
                IPAddress address = IPAddress.TryParse(ip, out var parsedAddress) ? parsedAddress : IPAddress.Loopback;
                using var socket = new UdpClient(new IPEndPoint(address, port));
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        private void ConfigureConnectionApproval()
        {
            if (NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            if (NetworkManager.Singleton.ConnectionApprovalCallback == null)
                NetworkManager.Singleton.ConnectionApprovalCallback = HandleConnectionApproval;
            else if (NetworkManager.Singleton.ConnectionApprovalCallback != HandleConnectionApproval)
                Debug.LogWarning("[GameNetworkManager] 다른 ConnectionApprovalCallback이 이미 등록되어 있습니다.");
        }

        private void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = true;
            response.CreatePlayerObject = false;
            response.Pending = false;
        }

        private void HandleClientConnected(ulong clientId)
        {
            OnClientConnected?.Invoke(clientId);
            Debug.Log($"[GameNetworkManager] 플레이어 {ConnectedPlayerCount}명 접속 (clientId: {clientId})");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            OnClientDisconnected?.Invoke(clientId);
            Debug.Log($"[GameNetworkManager] clientId {clientId} 종료. 현재 {ConnectedPlayerCount}명");
        }
    }
}
