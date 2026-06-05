using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Player;

namespace Vamsurlike.Skills
{
    // 전방 근접 스플래시. PlayerNetworkController.LastNonZeroMoveDirection 기준 판정.
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

            var controller = context.CasterTransform.GetComponent<PlayerNetworkController>();
            Vector3 forward = controller != null
                ? controller.LastNonZeroMoveDirection
                : context.CasterTransform.forward;

            Vector3 origin = context.CasterTransform.position;
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

                enemy.TakeDamage(damage);
                count++;
            }

            if (count > 0)
            {
                Debug.Log($"[{nameof(MeleeSkill)}] {count}마리 피격. damage={damage}, range={levelData.meleeRange}, arc={levelData.meleeArcAngle}°");
                context.Manager.BroadcastMeleeVFXClientRpc(origin, forward);
            }

            return count > 0;
        }
    }
}
