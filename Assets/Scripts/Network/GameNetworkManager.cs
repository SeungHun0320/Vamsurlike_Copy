using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Vamsurlike.Network
{
    public class GameNetworkManager : MonoBehaviour
    {
        public static GameNetworkManager Instance { get; private set; }

        public event Action<ulong> OnClientConnected;
        public event Action<ulong> OnClientDisconnected;

        public int ConnectedPlayerCount =>
            NetworkManager.Singleton?.ConnectedClients?.Count ?? 0;

        public bool IsClientConnected =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // Start() 사용 — OnEnable() 시점엔 NetworkManager.Singleton이 미초기화일 수 있음
        private void Start()
        {
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientConnectedCallback    += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback   += HandleClientDisconnected;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientConnectedCallback    -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback   -= HandleClientDisconnected;
        }

        // 로컬 / MPM 테스트용 직접 호스트
        public void StartAsHost(string ip = "127.0.0.1", ushort port = 7777)
        {
            SetTransport(ip, port);
            NetworkManager.Singleton.StartHost();
            Debug.Log($"[GameNetworkManager] Host 시작 — {ip}:{port}");
        }

        // 로컬 / 같은 LAN 클라이언트 접속
        public void StartAsClient(string ip = "127.0.0.1", ushort port = 7777)
        {
            SetTransport(ip, port);
            NetworkManager.Singleton.StartClient();
            Debug.Log($"[GameNetworkManager] Client 시작 — {ip}:{port}");
        }

        // 전용 서버 빌드 진입점 (UNITY_SERVER 또는 -batchmode)
        public void StartAsServer(string ip = "0.0.0.0", ushort port = 7777)
        {
            SetTransport(ip, port);
            NetworkManager.Singleton.StartServer();
            Debug.Log($"[GameNetworkManager] Server 시작 — {ip}:{port}");
        }

        public void Disconnect()
        {
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.Shutdown();
            Debug.Log("[GameNetworkManager] 연결 종료.");
        }

        private void SetTransport(string ip, ushort port)
        {
            var transport = NetworkManager.Singleton?.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError($"[{nameof(GameNetworkManager)}] UnityTransport를 찾을 수 없습니다.");
                return;
            }
            transport.SetConnectionData(ip, port);
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
