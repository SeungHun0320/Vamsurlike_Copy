using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Network;

namespace Vamsurlike.Skills
{
    // 합체 스킬: BasicProjectile + HealthRegen.
    // 투사체를 발사하며 위성들이 그 주위를 회전하면서 함께 날아간다(구 OrbitalGrenade 방식 재활용).
    // 본체 타격(NetworkProjectile.TryHitEnemy)과 위성 타격(OrbitingProjectileMode.TickDamage) 모두
    // lifestealRatio만큼 시전자를 회복시킨다.
    public sealed class LifeDrainBoltSkill : SkillBase
    {
        private const float DefaultSpawnHeight = 0.8f;

        public override SkillCastType SupportedCastType => SkillCastType.LifeDrainBolt;

        protected override bool Execute(in SkillCastContext context, Vector3 direction, EnemyNetworkBase _)
        {
            SkillDataSO    skill = context.Skill;
            SkillLevelData data  = context.LevelData;

            if (skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(LifeDrainBoltSkill)}] projectilePrefab 미할당. skill={skill.name}");
                return false;
            }

            Vector3 spawnPos = context.ProjectileSpawnPoint != null
                ? context.ProjectileSpawnPoint.position
                : context.CasterTransform.position + Vector3.up * DefaultSpawnHeight;
            spawnPos += direction * context.SpawnForwardOffset;

            Quaternion rot = skill.projectilePrefab.TryGetComponent<NetworkProjectile>(out var tmpl)
                ? tmpl.GetProjectileRotation(direction)
                : Quaternion.LookRotation(direction, Vector3.up);

            NetworkObject obj = PoolManager.GetOrInstantiateNetworkObject(
                skill.projectilePrefab,
                spawnPos,
                rot,
                nameof(LifeDrainBoltSkill));

            if (obj == null) return false;

            int   orbitalCount     = Mathf.Max(1, data.orbitalCount + context.BonusProjectileCount);
            float orbitalRadius    = Mathf.Max(0.1f, data.orbitalRadius * context.AreaMultiplier);
            float orbitalHitRadius = Mathf.Max(0.05f, data.orbitalHitRadius * context.AreaMultiplier);
            float orbitalDamage    = context.FinalDamage * Mathf.Max(0f, data.orbitalDamageMultiplier);

            NetworkProjectile proj = null;
            if (obj.TryGetComponent(out proj))
                proj.Initialize(skill.projectilePrefab, context.OwnerClientId, spawnPos, direction, data, context.FinalDamage, context.SpeedMultiplier, skill.name);
            else
                Debug.LogWarning($"[{nameof(LifeDrainBoltSkill)}] NetworkProjectile 없음. prefab={skill.projectilePrefab.name}");

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

            return true;
        }
    }
}
