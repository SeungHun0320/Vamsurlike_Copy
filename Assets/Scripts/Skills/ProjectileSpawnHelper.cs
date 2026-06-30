using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;

namespace Vamsurlike.Skills
{
    // 발사체 스폰 공통 로직. ProjectileSkill/PierceShotgunSkill/ScatterShotSkill/UltimateSkill 등이 공유.
    // Pool 조회 → 회전 결정 → Initialize → Spawn 순서를 중복 없이 처리.
    internal static class ProjectileSpawnHelper
    {
        // 발사체를 풀에서 꺼내 초기화·스폰한다. 실패 시 null 반환.
        internal static NetworkProjectile Spawn(
            GameObject prefab,
            ulong ownerClientId,
            Vector3 position,
            Vector3 direction,
            SkillLevelData levelData,
            float finalDamage,
            float speedMultiplier = 1f,
            string skillTag = null,
            string callerTag = "Skill")
        {
            Quaternion rot = prefab.TryGetComponent<NetworkProjectile>(out var tmpl)
                ? tmpl.GetProjectileRotation(direction)
                : Quaternion.LookRotation(direction, Vector3.up);

            NetworkObject obj = PoolManager.GetOrInstantiateNetworkObject(prefab, position, rot, callerTag);
            if (obj == null) return null;

            if (!obj.TryGetComponent<NetworkProjectile>(out var projectile))
            {
                Debug.LogWarning($"[{callerTag}] NetworkProjectile 없음. prefab={prefab.name}");
                return null;
            }

            projectile.Initialize(prefab, ownerClientId, position, direction, levelData, finalDamage, speedMultiplier, skillTag);
            obj.Spawn(true);
            return projectile;
        }
    }
}
