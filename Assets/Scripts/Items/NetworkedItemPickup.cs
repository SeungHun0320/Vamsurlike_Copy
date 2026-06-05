using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Network;
using Vamsurlike.Player;

namespace Vamsurlike.Items
{
    // 서버가 Spawn, 클라이언트가 범위 진입 → ServerRpc 픽업 요청
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedItemPickup : NetworkBehaviour
    {
        [SerializeField] private ItemDataSO itemData;
        private GameObject sourcePrefab;
        private bool        wasPoolSpawned;

        // 서버 전용 팩토리: DropManager에서 호출
        public static bool SpawnAt(ItemDataSO data, Vector3 position)
        {
            if (data == null || data.pickupPrefab == null) return false;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return false;

            bool usingPool = PoolManager.Instance != null;
            NetworkObject obj = usingPool
                ? PoolManager.Instance.GetNetworkObject(data.pickupPrefab, position, Quaternion.identity)
                : Object.Instantiate(data.pickupPrefab, position, Quaternion.identity)
                        .GetComponent<NetworkObject>();

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

            // 거리 검증
            const float MaxPickupDist = 4f;
            if (Vector3.SqrMagnitude(client.PlayerObject.transform.position - transform.position)
                > MaxPickupDist * MaxPickupDist) return;

            ApplyEffect(client.PlayerObject.gameObject, clientId);
            DespawnToPool();
        }

        private void ApplyEffect(GameObject playerObject, ulong clientId)
        {
            switch (itemData.itemType)
            {
                case ItemType.HealthOrb:
                    playerObject.GetComponent<PlayerNetworkStats>()?.Heal(itemData.value);
                    break;

                case ItemType.Missile:
                    ApplyMissileAoE();
                    break;

                case ItemType.Chest:
                    var chestManager = ChestRewardManager.Instance;
                    if (chestManager != null)
                        chestManager.BeginChestReward();
                    else
                        Debug.LogWarning($"[{nameof(NetworkedItemPickup)}] ChestRewardManager 없음");
                    break;
            }
        }

        private void ApplyMissileAoE()
        {
            float radius = itemData.missileRadius;
            var   cols   = Physics.OverlapSphere(transform.position, radius);
            foreach (var col in cols)
            {
                if (col.TryGetComponent<EnemyNetworkBase>(out var enemy))
                    enemy.TakeDamage(itemData.value);
            }
        }

        private void DespawnToPool()
        {
            if (!IsSpawned) return;
            // 풀에서 꺼낸 경우 destroy=false로 반환 대기, 직접 생성한 경우 즉시 파괴
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
