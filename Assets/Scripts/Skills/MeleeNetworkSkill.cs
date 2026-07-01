using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 전방 근접 스플래시. 범위 내 가장 가까운 적 방향으로 자동 조준, 없으면 CasterForward 사용.
    public sealed class MeleeSkill : SkillBase
    {
        private const float MinToEnemySqrMagnitude = 0.0001f;

        public override SkillCastType SupportedCastType => SkillCastType.Melee;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            Vector3 origin  = context.CasterTransform.position;
            Vector3 forward = ResolveForward(origin, levelData.meleeRange, context.CasterForward);

            float halfArc = levelData.meleeArcAngle * 0.5f;
            float damage = context.FinalDamage;
            int count = 0;

            var cols = Physics.OverlapSphere(origin, levelData.meleeRange);
            foreach (var col in cols)
            {
                if (!col.TryGetComponent<EnemyNetworkBase>(out var enemy)) continue;

                Vector3 toEnemy = col.transform.position - origin;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude < MinToEnemySqrMagnitude) continue;
                if (Vector3.Angle(forward, toEnemy.normalized) > halfArc) continue;

                enemy.TakeDamage(damage, context.OwnerClientId, context.Skill.name);
                count++;
            }

            context.VFX?.ShowMelee(origin, forward, levelData.meleeRange, levelData.meleeArcAngle);
            return true;
        }

        // 범위 내 가장 가까운 적 방향 반환. 없으면 fallback.
        private static Vector3 ResolveForward(Vector3 origin, float range, Vector3 fallback)
        {
            var nearest = AutoTargeting.FindNearestEnemy(origin, range);
            if (nearest == null) return fallback;

            Vector3 dir = nearest.transform.position - origin;
            dir.y = 0f;
            return dir.sqrMagnitude > MinToEnemySqrMagnitude ? dir.normalized : fallback;
        }
    }
}
