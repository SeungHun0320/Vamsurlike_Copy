using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    public class AuraNetworkSkill : SkillBase
    {
        private readonly List<EnemyNetworkBase> targets = new();

        protected override SkillCastType SupportedCastType => SkillCastType.AreaAura;
        public override bool IsPersistentExecution => true;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            float radius = levelData.areaRadius > 0f ? levelData.areaRadius : levelData.range;
            int targetCount = AutoTargeting.FindEnemiesInRange(context.CasterTransform.position, radius, targets);

            if (targetCount == 0)
            {
                if (ShouldLogNoTarget())
                    Debug.Log($"[{nameof(AuraNetworkSkill)}] No aura targets in range. skill={skill.name}, radius={radius}, position={context.CasterTransform.position}");

                return false;
            }

            for (int i = 0; i < targets.Count; i++)
                targets[i].TakeDamage(levelData.damage);

            Debug.Log($"[{nameof(AuraNetworkSkill)}] Aura tick. skill={skill.name}, level={context.Level}, damage={levelData.damage}, radius={radius}, targets={targetCount}");
            return true;
        }
    }
}
