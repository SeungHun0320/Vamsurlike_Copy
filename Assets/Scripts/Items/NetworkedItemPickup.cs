using System;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Network;
using Vamsurlike.Player;
using Vamsurlike.Stage;

namespace Vamsurlike.Items
{
    // 서버가 Spawn, 클라이언트가 범위 진입 → ServerRpc 픽업 요청
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedItemPickup : NetworkBehaviour
    {
        [SerializeField] private ItemDataSO itemData;
        private GameObject sourcePrefab;
        private bool        wasPoolSpawned;

        // 클라이언트: 서버가 픽업 요청을 거절했을 때 발생 — PlayerPickupController가 구독해 pendingPickupRequests 제거
        public static event Action<ulong> OnPickupRejected;

        // 서버 전용 팩토리: DropManager에서 호출
        public static bool SpawnAt(ItemDataSO data, Vector3 position)
        {
            if (data == null || data.pickupPrefab == null) return false;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return false;

            bool usingPool = PoolManager.Instance != null;
            NetworkObject obj = PoolManager.GetOrInstantiateNetworkObject(
                data.pickupPrefab,
                position,
                Quaternion.identity,
                nameof(NetworkedItemPickup));

            if (obj == null)
            {
                Debug.LogWarning($"[{nameof(NetworkedItemPickup)}] pickupPrefab에 NetworkObject가 없습니다. item={data.name}");
                return false;
            }

            if (!obj.TryGetComponent<NetworkedItemPickup>(out var pickup))
            {
                Debug.LogWarning($"[{nameof(NetworkedItemPickup)}] pickupPrefab에 {nameof(NetworkedItemPickup)} 컴포넌트가 없습니다. item={data.name}");
                return false;
            }

            pickup.itemData       = data;
            pickup.sourcePrefab   = data.pickupPrefab;
            pickup.wasPoolSpawned = usingPool;
            obj.Spawn(true);
            return true;
        }

        // Owner 클라이언트가 범위 안으로 들어오면 PlayerPickupController에서 호출
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestPickupRpc(RpcParams rpcParams = default)
        {
            if (itemData == null || !IsSpawned) return;

            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return;
            if (client.PlayerObject == null) return;

            // 비-상자 아이템: Gameplay 흐름에서만 허용 (선택/결과 화면 중 차단)
            // 상자는 GameFlowCoordinator 큐에 적재되므로 현재 Flow와 무관하게 항상 수거
            if (itemData.itemType != ItemType.Chest &&
                (GameFlowCoordinator.Instance == null ||
                 !GameFlowCoordinator.Instance.IsGameplayActive))
            {
                SendPickupRejected(clientId);
                return;
            }

            // 거리 검증
            const float MaxPickupDist = 4f;
            if (Vector3.SqrMagnitude(client.PlayerObject.transform.position - transform.position)
                > MaxPickupDist * MaxPickupDist) return;

            if (ApplyEffect(client.PlayerObject.gameObject))
                DespawnToPool();
            else
                SendPickupRejected(clientId);
        }

        // 서버 → 요청 클라이언트: 픽업 거절 통보 (pendingPickupRequests 정리 용도)
        [ClientRpc]
        private void NotifyPickupRejectedClientRpc(ClientRpcParams rpcParams = default)
        {
            OnPickupRejected?.Invoke(NetworkObjectId);
        }

        private void SendPickupRejected(ulong clientId)
        {
            NotifyPickupRejectedClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            });
        }

        private bool ApplyEffect(GameObject playerObject)
        {
            switch (itemData.itemType)
            {
                case ItemType.HealthOrb:
                    playerObject.GetComponent<PlayerNetworkStats>()?.Heal(itemData.value);
                    return true;

                case ItemType.Missile:
                    ApplyMissileAoE();
                    return true;

                case ItemType.Chest:
                    var chestManager = ChestRewardManager.Instance;
                    if (chestManager == null)
                    {
                        Debug.LogWarning($"[{nameof(NetworkedItemPickup)}] ChestRewardManager 없음");
                        return false;
                    }
                    return chestManager.BeginChestReward();

                default:
                    return false;
            }
        }

        private void ApplyMissileAoE()
        {
            float radius = itemData.missileRadius;
            var   cols   = Physics.OverlapSphere(transform.position, radius);
            foreach (var col in cols)
            {
                if (col.TryGetComponent<EnemyNetworkBase>(out var enemy))
                    enemy.TakeDamage(itemData.value); // 아이템 효과 — 플레이어 통계 비귀속
            }
        }

        private void DespawnToPool()
        {
            if (!IsSpawned) return;
            NetworkObject.Despawn(!wasPoolSpawned);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (!IsServer || !wasPoolSpawned || sourcePrefab == null) return;
            if (PoolManager.Instance != null)
                PoolManager.Instance.ReturnNetworkObject(sourcePrefab, NetworkObject);
        }
    }
}
