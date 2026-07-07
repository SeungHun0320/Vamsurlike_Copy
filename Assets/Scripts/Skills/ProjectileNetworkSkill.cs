using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    public sealed class ProjectileSkill : SkillBase
    {
        private const float DefaultSpawnHeight = 0.8f;

        // spreadAngle이 0인 스킬(기본 투사체 1레벨, 관통 투사체 등)에 보너스 투사체가 붙으면
        // 전부 같은 방향으로 겹쳐 날아가 늘어난 게 보이지 않는다 — 투사체 간 최소 퍼짐각을 보장.
        // 15°/간격은 기획 데이터(SD_BasicProjectile L2~L5: 18/15/14/13.75°)와 맞춘 값.
        private const float FallbackSpreadPerGap = 15f;

        public override SkillCastType SupportedCastType => SkillCastType.Projectile;

        protected override bool Execute(in SkillCastContext context, Vector3 direction, EnemyNetworkBase target)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(ProjectileSkill)}] projectilePrefab 미할당. skill={skill.name}");
                return false;
            }

            Vector3 spawnPos = context.ProjectileSpawnPoint != null
                ? context.ProjectileSpawnPoint.position
                : context.CasterTransform.position + Vector3.up * DefaultSpawnHeight;
            spawnPos += direction * context.SpawnForwardOffset;

            int count = Mathf.Max(1, levelData.projectileCount + context.BonusProjectileCount);
            float spread = Mathf.Max(0f, levelData.spreadAngle);
            if (count > 1 && spread <= 0f)
                spread = FallbackSpreadPerGap * (count - 1);
            int spawnedCount = 0;
            float finalDamage = context.FinalDamage;
            float speedMultiplier = context.SpeedMultiplier;

            for (int i = 0; i < count; i++)
            {
                Vector3 dir = GetSpreadDirection(direction, i, count, spread);
                if (SpawnProjectile(skill, levelData, finalDamage, context.OwnerClientId, spawnPos, dir, speedMultiplier))
                    spawnedCount++;
            }

            if (spawnedCount == 0) return false;

            return true;
        }

        private static bool SpawnProjectile(SkillDataSO skill, SkillLevelData levelData, float finalDamage,
            ulong ownerClientId, Vector3 pos, Vector3 dir, float speedMultiplier = 1f)
        {
            return ProjectileSpawnHelper.Spawn(
                skill.projectilePrefab, ownerClientId, pos, dir, levelData,
                finalDamage, speedMultiplier, skill.name, nameof(ProjectileSkill)) != null;
        }

        private static Vector3 GetSpreadDirection(Vector3 baseDir, int index, int count, float spreadAngle)
        {
            if (count <= 1 || spreadAngle <= 0f) return baseDir;
            float step = spreadAngle / (count - 1);
            float angle = -spreadAngle * 0.5f + step * index;
            return Quaternion.AngleAxis(angle, Vector3.up) * baseDir;
        }
    }
}
