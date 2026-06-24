using System;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Stage
{
    public enum StageClearCondition
    {
        TimeSurvival,
        BossKill,
        BothRequired,
    }

    [Serializable]
    public struct StageRow
    {
        public int                 stageId;
        public string              stageName;
        [Min(1f)] public float     durationSeconds;
        public int                 waveGroupId;
        public EnemyDataSO         bossData;
        public StageClearCondition clearCondition;
    }

    [CreateAssetMenu(menuName = "Vamsurlike/Data/StageTable", fileName = "StageTable")]
    public class StageTableSO : DataTableSO<StageRow>
    {
        public bool TryGetStage(int stageId, out StageRow row)
        {
            foreach (var r in Rows)
            {
                if (r.stageId == stageId) { row = r; return true; }
            }
            row = default;
            return false;
        }
    }
}
