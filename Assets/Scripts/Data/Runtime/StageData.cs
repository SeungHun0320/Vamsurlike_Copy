using Vamsurlike.Stage;

namespace Vamsurlike.Data.Runtime
{
    public sealed class StageData
    {
        public int                 Id              { get; }
        public string              StageName       { get; }
        public float               DurationSeconds { get; }
        public int                 WaveGroupId     { get; }
        public string              BossEnemyName   { get; }   // EnemySpawnManager 등록명
        public StageClearCondition ClearCondition  { get; }

        public bool HasBoss => !string.IsNullOrEmpty(BossEnemyName);

        public StageData(int id, string name, float duration, int waveGroupId,
                         string bossEnemyName, StageClearCondition clearCondition)
        {
            Id              = id;
            StageName       = name;
            DurationSeconds = duration;
            WaveGroupId     = waveGroupId;
            BossEnemyName   = bossEnemyName;
            ClearCondition  = clearCondition;
        }
    }
}
