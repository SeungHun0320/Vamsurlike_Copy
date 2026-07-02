using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 합체 스킬: ScatterShot + PierceProjectile.
    // 샷건처럼 한 번에 부채꼴로 퍼지며, levelData.pierceCount 만큼 관통한다.
    public sealed class PierceShotgunSkill : SkillBase
    {
        private const float DefaultSpawnHeight = 0.8f;

        public override SkillCastType SupportedCastType => SkillCastType.PierceShotgun;

        protected override bool Execute(in SkillCastContext context, Vector3 direction, EnemyNetworkBase _)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(PierceShotgunSkill)}] projectilePrefab 미할당. skill={skill.name}");
                return false;
            }

            Vector3 origin = context.CasterTransform.position + Vector3.up * DefaultSpawnHeight;
            int count = Mathf.Max(1, levelData.scatterBulletCount + context.BonusProjectileCount);
            float spreadAngle = Mathf.Max(0f, levelData.scatterAngle);

            for (int i = 0; i < count; i++)
            {
                Vector3 dir = GetSpreadDirection(direction, i, count, spreadAngle);
                SpawnBullet(skill.projectilePrefab, levelData, context.FinalDamage, context.OwnerClientId, origin, dir, context.SpeedMultiplier, skill.name);
            }

            return true;
        }

        private static Vector3 GetSpreadDirection(Vector3 baseForward, int index, int count, float spreadAngle)
        {
            if (count <= 1 || spreadAngle <= 0f) return baseForward;
            float step = spreadAngle / (count - 1);
            float angle = -spreadAngle * 0.5f + step * index;
            return Quaternion.AngleAxis(angle, Vector3.up) * baseForward;
        }

        private static void SpawnBullet(GameObject prefab, SkillLevelData levelData, float finalDamage,
            ulong ownerClientId, Vector3 position, Vector3 direction, float speedMultiplier, string skillTag = null)
        {
            ProjectileSpawnHelper.Spawn(
                prefab, ownerClientId, position, direction, levelData,
                finalDamage, speedMultiplier, skillTag, nameof(PierceShotgunSkill));
        }
    }
}
