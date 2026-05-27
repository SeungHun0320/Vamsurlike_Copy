using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Network
{
    // Enemy 프리팹에 부착. 서버 전용으로 거리 기반 NetworkShow/NetworkHide를 주기적으로 평가한다.
    [DisallowMultipleComponent]
    public class NetworkVisibilityController : NetworkBehaviour
    {
        [SerializeField] private float visibilityRange = 50f;
        [SerializeField] private float updateInterval  = 0.5f;

        private readonly HashSet<ulong> visibleClients = new();
        private float timer;

        private void Awake()
        {
            // Spawn() 전에 등록해야 초기 observer 판정에 반영됨 — OnNetworkSpawn은 이미 늦음
            if (TryGetComponent<NetworkObject>(out var netObj))
                netObj.CheckObjectVisibility = IsVisibleToClient;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) { enabled = false; return; }

            // 스폰 시점에 이미 접속 중인 클라이언트 가시 목록 초기화
            foreach (var kv in NetworkManager.Singleton.ConnectedClients)
            {
                if (IsVisibleToClient(kv.Key))
                    visibleClients.Add(kv.Key);
            }

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            timer = 0f;
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            visibleClients.Clear();
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = updateInterval;
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            float sqrRange = visibilityRange * visibilityRange;

            foreach (var kv in NetworkManager.Singleton.ConnectedClients)
            {
                ulong clientId  = kv.Key;
                var   player    = kv.Value.PlayerObject;
                if (player == null) continue;

                bool inRange    = Vector3.SqrMagnitude(player.transform.position - transform.position) <= sqrRange;
                bool wasVisible = visibleClients.Contains(clientId);

                if (inRange && !wasVisible)
                {
                    NetworkObject.NetworkShow(clientId);
                    visibleClients.Add(clientId);
                }
                else if (!inRange && wasVisible)
                {
                    NetworkObject.NetworkHide(clientId);
                    visibleClients.Remove(clientId);
                }
            }
        }

        private bool IsVisibleToClient(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return true;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                return false;
            if (client.PlayerObject == null) return true;

            float sqrRange = visibilityRange * visibilityRange;
            return Vector3.SqrMagnitude(client.PlayerObject.transform.position - transform.position) <= sqrRange;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            visibleClients.Remove(clientId);
        }
    }
}
