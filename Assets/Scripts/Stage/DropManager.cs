using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Stage
{
    // 서버 전용. 적 사망 보상(XP, 아이템) 처리 진입점.
    // Phase 6: DropTableSO 기반 아이템 드랍
    // Phase 7: 보스 보상
    public class DropManager : MonoBehaviour
    {
        public void OnEnemyDied(EnemyDataSO data, Vector3 position)
        {
            if (data == null) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            if (data.xpDrop > 0 && XPOrbManager.Instance != null)
                XPOrbManager.Instance.SpawnOrb(position, data.xpDrop);

            // Phase 6: if (data.dropTable != null) HandleItemDrops(data.dropTable, position);
        }
    }
}
