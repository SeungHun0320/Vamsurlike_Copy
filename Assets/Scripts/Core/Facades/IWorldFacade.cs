using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Core
{
    // 서버 전용 게임 상태. 클라이언트에서 호출하면 경고만 출력.
    public interface IWorldFacade
    {
        float GetStageElapsedTime();
        bool IsStageCleared();
        void SpawnEnemy(EnemyDataSO data, Vector3 pos);
        Vector3 GetRandomSpawnPoint();
        // OnEnemyDied(EnemyNetworkBase, ulong) → Phase 3 에서 추가
    }
}
