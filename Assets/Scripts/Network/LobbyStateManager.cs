using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Network
{
    // 클라이언트 접속/종료 시 LobbyPlayerState NetworkObject를 서버에서 관리.
    // Bootstrap 씬의 NetworkManager 오브젝트(또는 동일 씬의 오브젝트)에 부착.
    public class LobbyStateManager : MonoBehaviour
    {
        [SerializeField] private NetworkObject lobbyPlayerStatePrefab;

        private GameNetworkManager gnm;

        private void Start()
        {
            gnm = GameNetworkManager.Instance;
            if (gnm == null)
            {
                Debug.LogError($"[{nameof(LobbyStateManager)}] GameNetworkManager를 찾을 수 없습니다.", this);
                return;
            }

            gnm.OnClientConnected    += HandleClientConnected;
            gnm.OnClientDisconnected += HandleClientDisconnected;
            SpawnStatesForConnectedClients();
        }

        private void OnDestroy()
        {
            if (gnm == null) return;
            gnm.OnClientConnected    -= HandleClientConnected;
            gnm.OnClientDisconnected -= HandleClientDisconnected;
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServerRunning()) return;
            if (lobbyPlayerStatePrefab == null)
            {
                Debug.LogError($"[{nameof(LobbyStateManager)}] lobbyPlayerStatePrefab이 설정되지 않았습니다.", this);
                return;
            }
            if (LobbyPlayerState.All.ContainsKey(clientId)) return;

            var obj = Instantiate(lobbyPlayerStatePrefab);
            // destroyWithScene: false — 씬 전환 후에도 유지되어 플레이어 스폰 시 색상 읽기 가능
            obj.SpawnWithOwnership(clientId, destroyWithScene: false);
            Debug.Log($"[{nameof(LobbyStateManager)}] LobbyPlayerState 스폰 (clientId={clientId})");
        }

        private void SpawnStatesForConnectedClients()
        {
            if (!IsServerRunning()) return;
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                HandleClientConnected(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServerRunning()) return;
            if (!LobbyPlayerState.All.TryGetValue(clientId, out var state)) return;
            if (state != null && state.IsSpawned)
                state.NetworkObject.Despawn(destroy: true);
        }

        private static bool IsServerRunning()
            => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }
}
