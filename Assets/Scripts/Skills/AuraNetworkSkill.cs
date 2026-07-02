using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    public sealed class AuraSkill : SkillBase
    {
        private readonly List<EnemyNetworkBase> targets = new();
        private bool serverBroadcastSent;
        private float serverRadius;

        public override SkillCastType SupportedCastType => SkillCastType.AreaAura;
        public override bool IsPersistentExecution => true;

        public override void OnSkillRemoved(SkillCastType castType)
        {
            if (castType != SupportedCastType) return;

            serverBroadcastSent = false;
            serverRadius = 0f;
        }

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            float radius = (levelData.areaRadius > 0f ? levelData.areaRadius : levelData.range) * context.AreaMultiplier;
            if (!serverBroadcastSent || !Mathf.Approximately(serverRadius, radius))
            {
                serverBroadcastSent = true;
                serverRadius = radius;
                context.VFX?.ShowAreaCircle(SupportedCastType, radius, 0f, true);
            }

            int count = AutoTargeting.FindEnemiesInRange(context.CasterTransform.position, radius, targets);

            if (count == 0) return true; // 적 없어도 VFX 유지, 데미지만 생략

            float damage = context.FinalDamage;
            for (int i = 0; i < targets.Count; i++)
                targets[i].TakeDamage(damage, context.OwnerClientId, context.Skill.name);

            return true;
        }
    }
}
