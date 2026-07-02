using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    public sealed class OrbitalSkill : SkillBase
    {
        private readonly List<EnemyNetworkBase> targets = new();
        private readonly HashSet<ulong> hitEnemyIds = new();

        // 서버: ClientRpc 중복 전송 방지용 캐시
        private bool  serverBroadcastSent;
        private int   serverCount;
        private float serverRadius;
        private float serverRotSpeed;

        public override SkillCastType SupportedCastType => SkillCastType.Orbital;
        public override bool IsPersistentExecution => true;

        public override void OnSkillRemoved(SkillCastType castType)
        {
            if (castType != SupportedCastType) return;

            serverBroadcastSent = false;
            serverCount = 0;
            serverRadius = 0f;
            serverRotSpeed = 0f;
        }

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            int   count    = Mathf.Max(1, levelData.orbitalCount);
            float radius   = Mathf.Max(0.1f, levelData.orbitalRadius * context.AreaMultiplier);
            float hitRadius = Mathf.Max(0.05f, levelData.orbitalHitRadius);
            float rotSpeed = levelData.orbitalRotationSpeed;

            if (!serverBroadcastSent
                || serverCount    != count
                || !Mathf.Approximately(serverRadius,   radius)
                || !Mathf.Approximately(serverRotSpeed, rotSpeed))
            {
                serverBroadcastSent = true;
                serverCount    = count;
                serverRadius   = radius;
                serverRotSpeed = rotSpeed;
                context.VFX?.ShowOrbital(count, radius, rotSpeed);
            }

            int damagedCount = 0;
            hitEnemyIds.Clear();

            for (int i = 0; i < count; i++)
            {
                Vector3 orbPos = OrbitalPositionMath.Calculate(
                    context.CasterTransform.position, radius, rotSpeed, i, count);
                int targetCount = AutoTargeting.FindEnemiesInRange(orbPos, hitRadius, targets);

                float damage = context.FinalDamage;
                for (int j = 0; j < targetCount; j++)
                {
                    EnemyNetworkBase target = targets[j];
                    if (target == null || !hitEnemyIds.Add(target.NetworkObjectId)) continue;
                    target.TakeDamage(damage, context.OwnerClientId, context.Skill.name);
                    damagedCount++;
                }
            }

            if (damagedCount == 0) return true;

            return true;
        }
    }
}
