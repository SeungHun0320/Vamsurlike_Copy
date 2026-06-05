using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Network;

namespace Vamsurlike.Skills
{
    public sealed class ProjectileSkill : SkillBase
    {
        private const float DefaultSpawnHeight = 0.8f;
        private const float MinDirectionSqrMagnitude = 0.0001f;

        public override SkillCastType SupportedCastType => SkillCastType.Projectile;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            if (skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(ProjectileSkill)}] projectilePrefab 미할당. skill={skill.name}");
                return false;
            }

            EnemyNetworkBase target = AutoTargeting.FindNearestEnemy(context.CasterTransform.position, levelData.range);
            if (target == null)
            {
                if (ShouldLogNoTarget())
                    Debug.Log($"[{nameof(ProjectileSkill)}] 범위 내 적 없음. skill={skill.name}, range={levelData.range}");
                return false;
            }

            Vector3 spawnPos = context.ProjectileSpawnPoint != null
                ? context.ProjectileSpawnPoint.position
                : context.CasterTransform.position + Vector3.up * DefaultSpawnHeight;

            Vector3 direction = target.transform.position - spawnPos;
            direction.y = 0f;
            if (direction.sqrMagnitude < MinDirectionSqrMagnitude)
                direction = context.CasterTransform.forward;

            Vector3 baseDir = direction.normalized;
            spawnPos += baseDir * context.SpawnForwardOffset;

            int count = Mathf.Max(1, levelData.projectileCount);
            float spread = Mathf.Max(0f, levelData.spreadAngle);
            int spawnedCount = 0;
            float finalDamage = context.FinalDamage;

            float speedMultiplier = context.SpeedMultiplier;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = GetSpreadDirection(baseDir, i, count, spread);
                if (SpawnProjectile(skill, levelData, finalDamage, context.OwnerClientId, spawnPos, dir, speedMultiplier))
                    spawnedCount++;
            }

            if (spawnedCount == 0) return false;

            Debug.Log($"[{nameof(ProjectileSkill)}] 발사. skill={skill.name}, level={context.Level}, target={target.name}, damage={finalDamage}(x{context.AttackMultiplier}), count={spawnedCount}");
            return true;
        }

        private static bool SpawnProjectile(SkillDataSO skill, SkillLevelData levelData, float finalDamage,
            ulong ownerClientId, Vector3 pos, Vector3 dir, float speedMultiplier = 1f)
        {
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            if (skill.projectilePrefab.TryGetComponent<NetworkProjectile>(out var template))
                rot = template.GetProjectileRotation(dir);

            NetworkObject obj = PoolManager.Instance != null
                ? PoolManager.Instance.GetNetworkObject(skill.projectilePrefab, pos, rot)
                : Object.Instantiate(skill.projectilePrefab, pos, rot).GetComponent<NetworkObject>();

            if (obj == null) return false;

            if (obj.TryGetComponent<NetworkProjectile>(out var projectile))
                projectile.Initialize(skill.projectilePrefab, ownerClientId, pos, dir, levelData, finalDamage, speedMultiplier);
            else
                Debug.LogWarning($"[{nameof(ProjectileSkill)}] NetworkProjectile 없음. prefab={skill.projectilePrefab.name}");

            obj.Spawn(true);
            return true;
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
