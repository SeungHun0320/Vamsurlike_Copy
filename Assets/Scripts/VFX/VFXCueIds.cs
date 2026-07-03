namespace Vamsurlike.VFX
{
    // 씬 전역에서 공유하는 VFXCatalogSO cueId 상수. Assets/Data/VFX/VFXCatalog.asset의
    // entries[].cueId와 반드시 일치해야 한다.
    public static class VFXCueIds
    {
        public const int EnemyDeath   = 1;
        public const int BossDeath    = 2;
        public const int Ultimate     = 3;
        public const int PickupAbsorb = 4;
        public const int BossImpact   = 5;
    }
}
