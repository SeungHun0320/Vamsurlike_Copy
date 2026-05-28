using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Network;

namespace Vamsurlike.Skills
{
    public class ProjectileNetworkSkill : SkillBase
    {
        private const float DefaultSpawnHeight = 0.8f;
        private const float MinDirectionSqrMagnitude = 0.0001f;

        protected override SkillCastType SupportedCastType => SkillCastType.Projectile;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            if (skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(ProjectileNetworkSkill)}] Projectile skill has no projectile prefab. skill={skill.name}");
                return false;
            }

            EnemyNetworkBase target = AutoTargeting.FindNearestEnemy(context.CasterTransform.position, levelData.range);
            if (target == null)
            {
                if (ShouldLogNoTarget())
                    Debug.Log($"[{nameof(ProjectileNetworkSkill)}] No enemy target in range. skill={skill.name}, range={levelData.range}, position={context.CasterTransform.position}");

                return false;
            }

            Vector3 spawnPosition = context.ProjectileSpawnPoint != null
                ? context.ProjectileSpawnPoint.position
                : context.CasterTransform.position + Vector3.up * DefaultSpawnHeight;

            Vector3 direction = target.transform.position - spawnPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < MinDirectionSqrMagnitude)
                direction = context.CasterTransform.forward;

            Vector3 baseDirection = direction.normalized;
            spawnPosition += baseDirection * context.SpawnForwardOffset;

            int projectileCount = Mathf.Max(1, levelData.projectileCount);
            float spreadAngle = Mathf.Max(0f, levelData.spreadAngle);
            int spawnedCount = 0;

            for (int i = 0; i < projectileCount; i++)
            {
                Vector3 shotDirection = GetSpreadDirection(baseDirection, i, projectileCount, spreadAngle);
                if (SpawnProjectile(skill, levelData, context.OwnerClientId, spawnPosition, shotDirection))
                    spawnedCount++;
            }

            if (spawnedCount == 0)
                return false;

            Debug.Log($"[{nameof(ProjectileNetworkSkill)}] Fired projectile. skill={skill.name}, level={context.Level}, target={target.name}, damage={levelData.damage}, count={spawnedCount}, spawn={spawnPosition}");
            return true;
        }

        private bool SpawnProjectile(SkillDataSO skill, SkillLevelData levelData, ulong ownerClientId, Vector3 spawnPosition, Vector3 direction)
        {
            Quaternion spawnRotation = Quaternion.LookRotation(direction, Vector3.up);
            if (skill.projectilePrefab.TryGetComponent<NetworkProjectile>(out var projectilePrefab))
                spawnRotation = projectilePrefab.GetProjectileRotation(direction);

            NetworkObject projectileObject = PoolManager.Instance != null
                ? PoolManager.Instance.GetNetworkObject(skill.projectilePrefab, spawnPosition, spawnRotation)
                : Instantiate(skill.projectilePrefab, spawnPosition, spawnRotation).GetComponent<NetworkObject>();

            if (projectileObject == null)
                return false;

            if (projectileObject.TryGetComponent<NetworkProjectile>(out var projectile))
                projectile.Initialize(skill.projectilePrefab, ownerClientId, spawnPosition, direction, levelData);
            else
                Debug.LogWarning($"[{nameof(ProjectileNetworkSkill)}] Spawned projectile has no {nameof(NetworkProjectile)} component. prefab={skill.projectilePrefab.name}");

            projectileObject.Spawn(true);
            return true;
        }

        private static Vector3 GetSpreadDirection(Vector3 baseDirection, int index, int count, float spreadAngle)
        {
            if (count <= 1 || spreadAngle <= 0f)
                return baseDirection;

            float angleStep = spreadAngle / (count - 1);
            float startAngle = -spreadAngle * 0.5f;
            float angle = startAngle + angleStep * index;
            return Quaternion.AngleAxis(angle, Vector3.up) * baseDirection;
        }
    }
}
