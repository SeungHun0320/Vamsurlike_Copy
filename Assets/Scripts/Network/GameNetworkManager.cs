using System;
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

        public int ConnectedPlayerCount =>
            NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClients?.Count ?? 0 : 0;

        public bool IsClientConnected =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;

        public bool IsAvailableToStart =>
            NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening;

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
            NetworkManager.Singleton.OnClientConnectedCallback    += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback   += HandleClientDisconnected;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientConnectedCallback    -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback   -= HandleClientDisconnected;
        }

        public bool StartAsHost(string ip = "127.0.0.1", ushort port = 7777)
        {
            if (!CanStart("StartAsHost")) return false;
            if (!TrySetTransport(ip, port)) return false;
            bool ok = NetworkManager.Singleton.StartHost();
            Debug.Log($"[GameNetworkManager] Host 시작 — {ip}:{port} (ok={ok})");
            return ok;
        }

        // Relay 호스트 — SDK가 transport를 이미 설정했으므로 SetConnectionData 호출 안 함
        public bool StartAsRelayHost()
        {
            if (!CanStart("StartAsRelayHost")) return false;
            bool ok = NetworkManager.Singleton.StartHost();
            Debug.Log($"[GameNetworkManager] Relay Host 시작 (ok={ok})");
            return ok;
        }

        public bool StartAsClient(string ip = "127.0.0.1", ushort port = 7777)
        {
            if (!CanStart("StartAsClient")) return false;
            if (!TrySetTransport(ip, port)) return false;
            bool ok = NetworkManager.Singleton.StartClient();
            Debug.Log($"[GameNetworkManager] Client 시작 — {ip}:{port} (ok={ok})");
            return ok;
        }

        // Relay 클라이언트 — SDK가 transport를 이미 설정했으므로 SetConnectionData 호출 안 함
        public bool StartAsRelayClient()
        {
            if (!CanStart("StartAsRelayClient")) return false;
            bool ok = NetworkManager.Singleton.StartClient();
            Debug.Log($"[GameNetworkManager] Relay Client 시작 (ok={ok})");
            return ok;
        }

        public bool StartAsServer(string ip = "0.0.0.0", ushort port = 7777)
        {
            if (!CanStart("StartAsServer")) return false;
            if (!TrySetTransport(ip, port)) return false;
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
            return true;
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
