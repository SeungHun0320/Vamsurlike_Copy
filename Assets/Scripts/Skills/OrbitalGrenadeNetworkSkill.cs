using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 충격 궤도: Orbital + Defense.
    // 위성은 시각적으로만 공전하고, 데미지는 플레이어 중심 궤도 범위 안의 적에게 틱마다 1회 적용한다.
    // 진화 조건이 방어력 패시브라 투사체 개수 증가 패시브의 영향은 받지 않는다(orbitalCount 고정).
    public sealed class OrbitalGrenadeSkill : SkillBase
    {
        private readonly List<EnemyNetworkBase> targets = new();
        private readonly HashSet<ulong> hitEnemyIds = new();

        // 서버: ClientRpc 중복 전송 방지용 캐시
        private bool  serverBroadcastSent;
        private int   serverCount;
        private float serverRadius;
        private float serverRotSpeed;

        public override SkillCastType SupportedCastType => SkillCastType.OrbitalGrenade;
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

            int   count          = Mathf.Max(1, levelData.orbitalCount);
            float radius         = Mathf.Max(0.1f, levelData.orbitalRadius * context.AreaMultiplier);
            float hitRadius      = Mathf.Max(0.05f, levelData.orbitalHitRadius * context.AreaMultiplier);
            float rotSpeed       = levelData.orbitalRotationSpeed;
            float knockbackForce = Mathf.Max(0f, levelData.orbitalKnockbackForce);

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

            hitEnemyIds.Clear();
            Vector3 center = context.CasterTransform.position;
            float damageRange = radius + hitRadius;
            float damage = context.FinalDamage;
            int targetCount = AutoTargeting.FindEnemiesInRange(center, damageRange, targets);

            for (int i = 0; i < targetCount; i++)
            {
                EnemyNetworkBase target = targets[i];
                if (target == null || !hitEnemyIds.Add(target.NetworkObjectId)) continue;

                target.TakeDamage(damage, context.OwnerClientId, skill.name);

                if (knockbackForce > 0f)
                    target.ApplyKnockback(center, knockbackForce);
            }

            return true;
        }
    }
}
