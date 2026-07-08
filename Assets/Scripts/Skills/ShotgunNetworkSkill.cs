using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 원뿔형 범위에 즉시 판정 후 사라지는 샷건(SpreadProjectile 재구성). 관통 없음 — 원뿔 내 가장 가까운 적 1명만 타격.
    public sealed class ShotgunSkill : SkillBase
    {
        private const float MinToEnemySqrMagnitude = 0.0001f;

        public override SkillCastType SupportedCastType => SkillCastType.Shotgun;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            Vector3 origin  = context.CasterTransform.position;
            float   range   = AutoTargeting.ResolveTargetingRange(context);
            Vector3 forward = AutoTargeting.ResolveDirection(context, origin, context.CasterForward, out _);
            float   halfArc = levelData.spreadAngle * 0.5f;
            float   damage  = context.FinalDamage;

            EnemyNetworkBase nearest = null;
            float bestSqrDist = float.MaxValue;

            var cols = Physics.OverlapSphere(origin, range);
            foreach (var col in cols)
            {
                if (!col.TryGetComponent<EnemyNetworkBase>(out var enemy)) continue;

                Vector3 toEnemy = col.transform.position - origin;
                toEnemy.y = 0f;
                float sqrDist = toEnemy.sqrMagnitude;
                if (sqrDist < MinToEnemySqrMagnitude) continue;
                if (Vector3.Angle(forward, toEnemy.normalized) > halfArc) continue;
                if (sqrDist >= bestSqrDist) continue;

                bestSqrDist = sqrDist;
                nearest = enemy;
            }

            nearest?.TakeDamage(damage, context.OwnerClientId, skill.name);

            context.VFX?.ShowMelee(SupportedCastType, origin, forward, range, levelData.spreadAngle);
            return true;
        }
    }
}
