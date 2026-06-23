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
            int count = Mathf.Max(1, levelData.scatterBulletCount);
            float spreadAngle = Mathf.Max(0f, levelData.scatterAngle);

            for (int i = 0; i < count; i++)
            {
                Vector3 dir = GetSpreadDirection(direction, i, count, spreadAngle);
                SpawnBullet(skill.projectilePrefab, levelData, context.FinalDamage, context.OwnerClientId, origin, dir, context.SpeedMultiplier);
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
