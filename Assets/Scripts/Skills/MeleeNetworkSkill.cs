using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 전방 근접 스플래시(사각형 판정). 범위 내 가장 가까운 적 방향으로 자동 조준, 없으면 CasterForward 사용.
    // 원뿔(샷건)과 판정 형태를 구분하기 위해 전방 사각형 박스로 판정한다.
    public sealed class MeleeSkill : SkillBase
    {
        public override SkillCastType SupportedCastType => SkillCastType.Melee;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            Vector3 origin  = context.CasterTransform.position;
            float   range   = AutoTargeting.ResolveTargetingRange(context);
            Vector3 forward = AutoTargeting.ResolveDirection(context, origin, context.CasterForward, out _);
            float   width   = levelData.meleeWidth * context.AreaMultiplier;

            float damage = context.FinalDamage;
            MeleeBoxHit.Apply(origin, forward, range, width, damage, context.OwnerClientId, context.Skill.name);

            context.VFX?.ShowMeleeBox(SupportedCastType, origin, forward, range, width);
            return true;
        }

    }
}
