namespace Vamsurlike.Data.Runtime
{
    public sealed class EnemyScalingData
    {
        public int   Id                  { get; }
        public float TimeMinutes         { get; }
        public float HpMultiplier        { get; }
        public float DamageMultiplier    { get; }
        public float SpawnRateMultiplier { get; }

        public EnemyScalingData(int id, float timeMinutes, float hp, float dmg, float spawn)
        {
            Id                  = id;
            TimeMinutes         = timeMinutes;
            HpMultiplier        = hp;
            DamageMultiplier    = dmg;
            SpawnRateMultiplier = spawn;
        }
    }
}
