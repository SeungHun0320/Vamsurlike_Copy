using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    // 합체 스킬: ScatterShot + PierceProjectile.
    // ScatterShotSkill과 동일한 산탄 로직. levelData.pierceCount 로 관통 적용.
    public sealed class PierceShotgunSkill : ScatterShotSkill
    {
        public override SkillCastType SupportedCastType => SkillCastType.PierceShotgun;
    }
}
