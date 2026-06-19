using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Network;

namespace Vamsurlike.Skills
{
    // 합체 스킬: ScatterShot + PierceProjectile.
    // 샷건처럼 한 번에 부채꼴로 퍼지며, levelData.pierceCount 만큼 관통한다.
    public sealed class PierceShotgunSkill : SkillBase
    {
        private const float DefaultSpawnHeight = 0.8f;
        private const float MinDirectionSqrMagnitude = 0.0001f;

        public override SkillCastType SupportedCastType => SkillCastType.PierceShotgun;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            if (skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(PierceShotgunSkill)}] projectilePrefab 미할당. skill={skill.name}");
                return false;
            }

            EnemyNetworkBase target = AutoTargeting.FindNearestEnemy(context.CasterTransform.position, levelData.range);
            if (target == null)
            {
                if (ShouldLogNoTarget())
                    Debug.Log($"[{nameof(PierceShotgunSkill)}] 범위 내 적 없음. skill={skill.name}, range={levelData.range}");
                return false;
            }

            Vector3 baseForward = target.transform.position - context.CasterTransform.position;
            baseForward.y = 0f;
            if (baseForward.sqrMagnitude < MinDirectionSqrMagnitude)
                baseForward = context.CasterTransform.forward;
            baseForward.Normalize();

            Vector3 origin = context.CasterTransform.position + Vector3.up * DefaultSpawnHeight;
            int count = Mathf.Max(1, levelData.scatterBulletCount);
            float spreadAngle = Mathf.Max(0f, levelData.scatterAngle);

            for (int i = 0; i < count; i++)
            {
                Vector3 dir = GetSpreadDirection(baseForward, i, count, spreadAngle);
                SpawnBullet(skill.projectilePrefab, levelData, context.FinalDamage, context.OwnerClientId, origin, dir, context.SpeedMultiplier);
            }

            Debug.Log($"[{nameof(PierceShotgunSkill)}] 발사. skill={skill.name}, count={count}, spread={spreadAngle}, pierce={levelData.pierceCount}");
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
            ulong ownerClientId, Vector3 position, Vector3 direction, float speedMultiplier)
        {
            Quaternion rot = Quaternion.LookRotation(direction, Vector3.up);
            if (prefab.TryGetComponent<NetworkProjectile>(out var template))
                rot = template.GetProjectileRotation(direction);

            NetworkObject obj = PoolManager.Instance != null
                ? PoolManager.Instance.GetNetworkObject(prefab, position, rot)
                : Object.Instantiate(prefab, position, rot).GetComponent<NetworkObject>();

            if (obj == null) return;

            if (obj.TryGetComponent<NetworkProjectile>(out var projectile))
                projectile.Initialize(prefab, ownerClientId, position, direction, levelData, finalDamage, speedMultiplier);
            else
                Debug.LogWarning($"[{nameof(PierceShotgunSkill)}] NetworkProjectile 없음. prefab={prefab.name}");

            obj.Spawn(true);
        }
    }
}
