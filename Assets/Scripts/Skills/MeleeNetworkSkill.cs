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
            float   range   = AutoTargeting.ResolveTargetingRange(context);
            Vector3 forward = AutoTargeting.ResolveDirection(context, origin, context.CasterForward, out _);

            float halfArc = levelData.meleeArcAngle * 0.5f;
            float damage = context.FinalDamage;
            int count = 0;

            var cols = Physics.OverlapSphere(origin, range);
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

            context.VFX?.ShowMelee(origin, forward, range, levelData.meleeArcAngle);
            return true;
        }

    }
}
