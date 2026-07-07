namespace Vamsurlike.Audio
{
    // 씬 전역에서 공유하는 SFXCatalogSO cueId 상수. Assets/Data/Audio/SFXCatalog.asset의
    // entries[].cueId와 반드시 일치해야 한다.
    public static class SFXCueIds
    {
        public const int Hit             = 1;
        public const int EnemyDeath      = 2;
        public const int BossDeath       = 3;
        public const int SkillCast       = 4;
        public const int ItemPickup      = 5;
        public const int XPPickup        = 6;
        public const int GoldPickup      = 7;
        public const int ChestOpen       = 8;
        public const int LevelUp         = 9;
        public const int BossTelegraph   = 10;
        public const int BossImpact      = 11;
    }
}
