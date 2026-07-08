using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Network
{
    // Enemy 프리팹에 부착. 서버 전용으로 거리 기반 NetworkShow/NetworkHide를 주기적으로 평가한다.
    [DisallowMultipleComponent]
    public class NetworkVisibilityController : ServerBehaviour
    {
        // 카메라 FollowOffset(-18,30,-18)+FOV 40 기준 실측 가로 가시폭 ≈ 50유닛.
        // range를 그 값과 똑같이 두면 화면 가장자리에서 바로 Show/Hide가 갈려 팝인이 보이므로
        // 안전 마진(약 30%)을 둔다. updateInterval은 적 이동속도(3.5) 기준 판정 주기당 최대
        // 1.75유닛 이동이라 range 대비 여유가 충분해 그대로 유지.
        [SerializeField] private float visibilityRange = 65f;
        [SerializeField] private float updateInterval  = 0.5f;

        private readonly HashSet<ulong> visibleClients = new();
        private float timer;

        private void Awake()
        {
            // Spawn() 전에 등록해야 초기 observer 판정에 반영됨 — OnNetworkSpawn은 이미 늦음
            if (TryGetComponent<NetworkObject>(out var netObj))
                netObj.CheckObjectVisibility = IsVisibleToClient;
        }

        protected override void OnServerSpawned()
        {
            foreach (var kv in NetworkManager.Singleton.ConnectedClients)
            {
                if (IsVisibleToClient(kv.Key))
                    visibleClients.Add(kv.Key);
            }
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            timer = 0f;
        }

        protected override void OnServerDespawned()
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
