namespace Vamsurlike.Upgrades
{
    // Phase 7.6 — 골드로 구매하는 영구(세션 한정) 업그레이드. Phase 7.5의 UpgradeEffectType(인게임 한정
    // 패시브)과 이름은 겹치지만 완전히 별개 시스템이다. PermanentUpgradeTable의 행 순서와 반드시 일치시킬 것.
    public enum PermanentUpgradeType
    {
        Damage,
        Defense,
        MaxHP,
        HealthRegen,
        MoveSpeed,
        PickupRadius,
        AreaSize,
        Duration,
        CritChance,
        SkillHaste,
        XPGain,
        ProjectileCount,
        GoldGain,
    }
}
