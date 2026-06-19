using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Network;

namespace Vamsurlike.Skills
{
    // 합체 스킬: Orbital + BasicProjectile.
    // 투사체를 발사하며 orbitalCount개 위성이 주위를 회전하면서 날아감.
    // 위성은 경로상의 적에게 프레임마다 초당 데미지(damage/s)를 적용.
    public sealed class OrbitalGrenadeSkill : SkillBase
    {
        private const float DefaultSpawnHeight = 0.8f;
        private const float MinDirSqrMag       = 0.0001f;

        public override SkillCastType SupportedCastType => SkillCastType.OrbitalGrenade;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO    skill = context.Skill;
            SkillLevelData data  = context.LevelData;

            if (skill == null || data == null || context.CasterTransform == null) return false;
            if (skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(OrbitalGrenadeSkill)}] projectilePrefab 미할당. skill={skill.name}");
                return false;
            }

            EnemyNetworkBase target = AutoTargeting.FindNearestEnemy(context.CasterTransform.position, data.range);
            if (target == null)
            {
                if (ShouldLogNoTarget())
                    Debug.Log($"[{nameof(OrbitalGrenadeSkill)}] 범위 내 적 없음. range={data.range}");
                return false;
            }

            Vector3 spawnPos = context.ProjectileSpawnPoint != null
                ? context.ProjectileSpawnPoint.position
                : context.CasterTransform.position + Vector3.up * DefaultSpawnHeight;

            Vector3 dir = target.transform.position - spawnPos;
            dir.y = 0f;
            if (dir.sqrMagnitude < MinDirSqrMag) dir = context.CasterTransform.forward;
            dir.Normalize();
            spawnPos += dir * context.SpawnForwardOffset;

            Quaternion rot = skill.projectilePrefab.TryGetComponent<NetworkProjectile>(out var tmpl)
                ? tmpl.GetProjectileRotation(dir)
                : Quaternion.LookRotation(dir, Vector3.up);

            NetworkObject obj = PoolManager.Instance != null
                ? PoolManager.Instance.GetNetworkObject(skill.projectilePrefab, spawnPos, rot)
                : Object.Instantiate(skill.projectilePrefab, spawnPos, rot).GetComponent<NetworkObject>();

            if (obj == null) return false;

            NetworkProjectile proj = null;
            int orbitalCount = Mathf.Max(1, data.orbitalCount);
            float orbitalRadius = Mathf.Max(0.1f, data.orbitalRadius);
            float orbitalHitRadius = Mathf.Max(0.05f, data.orbitalHitRadius);
            float orbitalDamage = context.FinalDamage * Mathf.Max(0f, data.orbitalDamageMultiplier);

            if (obj.TryGetComponent(out proj))
            {
                proj.Initialize(skill.projectilePrefab, context.OwnerClientId, spawnPos, dir, data, context.FinalDamage, context.SpeedMultiplier);
            }
            else
            {
                Debug.LogWarning($"[{nameof(OrbitalGrenadeSkill)}] NetworkProjectile 없음. prefab={skill.projectilePrefab.name}");
            }

            obj.Spawn(true);
            if (proj != null)
            {
                proj.SpawnOrbitingProjectiles(
                    skill.projectilePrefab,
                    orbitalCount,
                    orbitalRadius,
                    data.orbitalRotationSpeed,
                    orbitalHitRadius,
                    orbitalDamage);
            }

            Debug.Log($"[{nameof(OrbitalGrenadeSkill)}] 발사. target={target.name}, orbCount={data.orbitalCount}, damage={context.FinalDamage}");
            return true;
        }
    }
}
