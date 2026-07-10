namespace Vamsurlike.Data.Runtime
{
    public sealed class EnemyScalingData
    {
        public int   Id                  { get; }
        public float TimeMinutes         { get; }
        public float HpMultiplier        { get; }
        public float DamageMultiplier    { get; }
        public float SpawnRateMultiplier { get; }
        public float XpMultiplier        { get; }

        public EnemyScalingData(int id, float timeMinutes, float hp, float dmg, float spawn, float xp = 1f)
        {
            Id                  = id;
            TimeMinutes         = timeMinutes;
            HpMultiplier        = hp;
            DamageMultiplier    = dmg;
            SpawnRateMultiplier = spawn;
            XpMultiplier        = xp;
        }
    }
}
