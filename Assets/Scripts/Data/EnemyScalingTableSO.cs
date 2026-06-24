using System;
using UnityEngine;

namespace Vamsurlike.Data
{
    [Serializable]
    public struct ScalingRow
    {
        [Min(0f)] public float timeMinutes;
        [Min(0.1f)] public float hpMultiplier;
        [Min(0.1f)] public float damageMultiplier;
        [Min(0.1f)] public float spawnRateMultiplier;
    }

    [CreateAssetMenu(menuName = "Vamsurlike/Data/EnemyScalingTable", fileName = "EnemyScalingTable")]
    public class EnemyScalingTableSO : DataTableSO<ScalingRow> { }
}
