using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Items;

namespace Vamsurlike.Stage
{
    // 서버 전용. 적 사망 보상(XP, 아이템) 처리 진입점.
    public class DropManager : MonoBehaviour
    {
        private const float GoldScatterRadius = 0.6f;

        private readonly System.Random rng = new();

        public void OnEnemyDied(EnemyDataSO data, Vector3 position)
        {
            if (data == null) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            if (data.xpDrop > 0 && XPOrbManager.Instance != null)
                XPOrbManager.Instance.SpawnOrb(position, data.xpDrop);

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

            if (!NetworkedItemPickup.SpawnAt(item, position))
                Debug.LogWarning($"[{nameof(DropManager)}] 아이템 드랍 스폰 실패: {item.name}", this);
        }

        // Phase 7.6 — 골드는 아이템과 별개로 판정 (같은 적이 아이템+골드 동시 드랍 가능)
        private void HandleGoldDrop(DropTableSO table, Vector3 position)
        {
            int gold = table.RollGold(rng);
            if (gold <= 0 || GoldOrbManager.Instance == null) return;

            GoldOrbManager.Instance.SpawnOrb(ScatterPosition(position, GoldScatterRadius), gold);
        }

        // 여러 적이 비슷한 위치에서 죽을 때 골드 오브가 완전히 겹쳐 보이는 것을 방지하기 위해
        // 사망 지점 기준 원형 범위 내 랜덤 오프셋을 준다 (지면은 XZ 평면, Y는 그대로 유지).
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
