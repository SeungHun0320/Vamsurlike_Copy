using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Core
{
    public interface IWorldFacade
    {
        float   GetStageElapsedTime();
        bool    IsStageCleared();
        void    OnEnemyDied(EnemyBase enemy);
        void    SpawnEnemy(EnemyDataSO data, Vector3 pos);
        Vector3 GetRandomSpawnPoint();
    }
}
