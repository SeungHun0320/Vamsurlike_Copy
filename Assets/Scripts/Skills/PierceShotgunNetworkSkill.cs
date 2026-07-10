using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 합체 스킬: ScatterShot + PierceProjectile.
    // 원뿔형 범위에 즉시 판정 후 사라진다. 관통 = 원뿔 내 모든 적을 동시 타격.
    public sealed class PierceShotgunSkill : SkillBase
    {
        private const float MinToEnemySqrMagnitude = 0.0001f;
        private static readonly Collider[] OverlapBuffer = new Collider[256];

        public override SkillCastType SupportedCastType => SkillCastType.PierceShotgun;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            Vector3 origin  = context.CasterTransform.position;
            float   range   = AutoTargeting.ResolveTargetingRange(context);
            Vector3 forward = AutoTargeting.ResolveDirection(context, origin, context.CasterForward, out _);
            float   halfArc = levelData.scatterAngle * 0.5f;
            float   damage  = context.FinalDamage;

            int overlapCount = Physics.OverlapSphereNonAlloc(origin, range, OverlapBuffer);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null || !col.TryGetComponent<EnemyNetworkBase>(out var enemy))
                {
                    OverlapBuffer[i] = null;
                    continue;
                }

                Vector3 toEnemy = col.transform.position - origin;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude >= MinToEnemySqrMagnitude && Vector3.Angle(forward, toEnemy.normalized) <= halfArc)
                    enemy.TakeDamage(damage, context.OwnerClientId, skill.name);

                OverlapBuffer[i] = null;
            }

            context.VFX?.ShowMelee(SupportedCastType, origin, forward, range, levelData.scatterAngle);
            return true;
        }
    }
}
