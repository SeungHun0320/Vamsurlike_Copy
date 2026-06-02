using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Items;

namespace Vamsurlike.Stage
{
    // 서버 전용. 적 사망 보상(XP, 아이템) 처리 진입점.
    public class DropManager : MonoBehaviour
    {
        private readonly System.Random rng = new();

        public void OnEnemyDied(EnemyDataSO data, Vector3 position)
        {
            if (data == null) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            if (data.xpDrop > 0 && XPOrbManager.Instance != null)
                XPOrbManager.Instance.SpawnOrb(position, data.xpDrop);

            if (data.dropTable != null)
                HandleItemDrop(data.dropTable, position);
        }

        private void HandleItemDrop(DropTableSO table, Vector3 position)
        {
            var item = table.Roll(rng);
            if (item == null || item.pickupPrefab == null) return;

            if (!NetworkedItemPickup.SpawnAt(item, position))
                Debug.LogWarning($"[{nameof(DropManager)}] 아이템 드랍 스폰 실패: {item.name}", this);
        }
    }
}
