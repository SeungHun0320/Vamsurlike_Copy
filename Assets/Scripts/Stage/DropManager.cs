using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Items;

namespace Vamsurlike.Stage
{
    // 서버 전용. 적 사망 보상(XP, 아이템) 처리 진입점.
    public class DropManager : MonoBehaviour
    {
        // 같은 지점(또는 근처)에서 여러 적이 죽을 때 XP/아이템/골드가 전부 한 자리에 겹쳐
        // 보이지 않도록 모든 드랍 타입에 공통으로 적용하는 스캐터 반경.
        private const float DropScatterRadius = 0.6f;

        private readonly System.Random rng = new();

        public void OnEnemyDied(EnemyDataSO data, Vector3 position)
        {
            if (data == null) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            if (data.xpDrop > 0 && XPOrbManager.Instance != null)
                XPOrbManager.Instance.SpawnOrb(ScatterPosition(position, DropScatterRadius), data.xpDrop);

            if (data.dropTable != null)
            {
                HandleItemDrop(data.dropTable, position);
                HandleGoldDrop(data.dropTable, position);
            }
        }

        private void HandleItemDrop(DropTableSO table, Vector3 position)
        {
            var item = table.Roll(rng);
            if (item == null || item.pickupPrefab == null) return;

            if (!NetworkedItemPickup.SpawnAt(item, ScatterPosition(position, DropScatterRadius)))
                Debug.LogWarning($"[{nameof(DropManager)}] 아이템 드랍 스폰 실패: {item.name}", this);
        }

        // Phase 7.6 — 골드는 아이템과 별개로 판정 (같은 적이 아이템+골드 동시 드랍 가능)
        private void HandleGoldDrop(DropTableSO table, Vector3 position)
        {
            int gold = table.RollGold(rng);
            if (gold <= 0 || GoldOrbManager.Instance == null) return;

            GoldOrbManager.Instance.SpawnOrb(ScatterPosition(position, DropScatterRadius), gold);
        }

        // 사망 지점 기준 원형 범위 내 랜덤 오프셋을 준다 (지면은 XZ 평면, Y는 그대로 유지).
        // XP/아이템/골드 각각 독립적으로 호출되므로 같은 사망 지점에서 나온 드랍끼리도 서로 흩어진다.
        private Vector3 ScatterPosition(Vector3 origin, float radius)
        {
            double angle = rng.NextDouble() * System.Math.PI * 2.0;
            double dist  = rng.NextDouble() * radius;
            float offsetX = (float)(System.Math.Cos(angle) * dist);
            float offsetZ = (float)(System.Math.Sin(angle) * dist);
            return origin + new Vector3(offsetX, 0f, offsetZ);
        }
    }
}
