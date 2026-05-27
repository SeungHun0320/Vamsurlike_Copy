using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Network;

namespace Vamsurlike.Skills
{
    [RequireComponent(typeof(NetworkObject))]
    public class SkillManager : NetworkBehaviour
    {
        [Serializable]
        private class OwnedSkill
        {
            public SkillDataSO skill;
            [Min(1)] public int level = 1;
            public float cooldownTimer;

            public OwnedSkill(SkillDataSO skill, int level)
            {
                this.skill = skill;
                this.level = Mathf.Max(1, level);
            }
        }

        [SerializeField] private SkillDataSO[] startingSkills;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float spawnForwardOffset = 0.8f;
        [SerializeField] private float failedCastRetryDelay = 0.1f;

        private readonly List<OwnedSkill> ownedSkills = new();
        private readonly List<EnemyNetworkBase> auraTargets = new();
        private float nextNoTargetLogTime;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                Debug.Log($"[{nameof(SkillManager)}] Disabled on client. owner={OwnerClientId}, object={name}");
                enabled = false;
                return;
            }

            InitializeStartingSkills();
            Debug.Log($"[{nameof(SkillManager)}] Spawned on server. owner={OwnerClientId}, object={name}, skillCount={ownedSkills.Count}");

            if (ownedSkills.Count == 0)
                Debug.LogWarning($"[{nameof(SkillManager)}] No starting skills assigned. object={name}");
        }

        private void Update()
        {
            if (!IsServer) return;

            for (int i = 0; i < ownedSkills.Count; i++)
            {
                OwnedSkill ownedSkill = ownedSkills[i];
                if (ownedSkill.skill == null)
                {
                    if (Time.time >= nextNoTargetLogTime)
                    {
                        Debug.LogWarning($"[{nameof(SkillManager)}] ownedSkills[{i}] is null. object={name}");
                        nextNoTargetLogTime = Time.time + 2f;
                    }
                    continue;
                }

                ownedSkill.cooldownTimer -= Time.deltaTime;
                if (ownedSkill.cooldownTimer > 0f) continue;

                SkillLevelData levelData = ownedSkill.skill.GetLevelData(ownedSkill.level);
                ownedSkill.cooldownTimer = TryCast(ownedSkill, levelData)
                    ? levelData != null ? levelData.cooldown : 1f
                    : failedCastRetryDelay;
            }
        }

        public bool LearnSkill(SkillDataSO skill)
        {
            if (!IsServer || skill == null) return false;
            if (TryGetOwnedSkill(skill, out var ownedSkill))
                return UpgradeSkill(skill);

            ownedSkills.Add(new OwnedSkill(skill, 1));
            Debug.Log($"[{nameof(SkillManager)}] Learned skill. owner={OwnerClientId}, skill={skill.name}");
            return true;
        }

        public bool UpgradeSkill(SkillDataSO skill)
        {
            if (!IsServer || skill == null) return false;
            if (!TryGetOwnedSkill(skill, out var ownedSkill)) return LearnSkill(skill);

            int maxLevel = Mathf.Max(1, skill.maxLevel);
            if (ownedSkill.level >= maxLevel) return false;

            ownedSkill.level++;
            ownedSkill.cooldownTimer = Mathf.Min(ownedSkill.cooldownTimer, failedCastRetryDelay);
            Debug.Log($"[{nameof(SkillManager)}] Upgraded skill. owner={OwnerClientId}, skill={skill.name}, level={ownedSkill.level}");
            return true;
        }

        public int GetSkillLevel(SkillDataSO skill)
        {
            return TryGetOwnedSkill(skill, out var ownedSkill) ? ownedSkill.level : 0;
        }

        private void InitializeStartingSkills()
        {
            ownedSkills.Clear();
            if (startingSkills == null) return;

            for (int i = 0; i < startingSkills.Length; i++)
            {
                SkillDataSO skill = startingSkills[i];
                if (skill == null)
                {
                    Debug.LogWarning($"[{nameof(SkillManager)}] startingSkills[{i}] is null. object={name}");
                    continue;
                }

                if (TryGetOwnedSkill(skill, out var ownedSkill))
                {
                    ownedSkill.level = Mathf.Min(ownedSkill.level + 1, Mathf.Max(1, skill.maxLevel));
                    continue;
                }

                ownedSkills.Add(new OwnedSkill(skill, 1));
            }
        }

        private bool TryGetOwnedSkill(SkillDataSO skill, out OwnedSkill ownedSkill)
        {
            for (int i = 0; i < ownedSkills.Count; i++)
            {
                if (ownedSkills[i].skill != skill) continue;
                ownedSkill = ownedSkills[i];
                return true;
            }

            ownedSkill = null;
            return false;
        }

        private bool TryCast(OwnedSkill ownedSkill, SkillLevelData levelData)
        {
            SkillDataSO skill = ownedSkill.skill;
            if (skill == null || levelData == null) return false;

            return skill.castType switch
            {
                SkillCastType.Projectile => TryCastProjectile(ownedSkill, levelData),
                SkillCastType.AreaAura => TryCastAreaAura(ownedSkill, levelData),
                _ => false
            };
        }

        private bool TryCastProjectile(OwnedSkill ownedSkill, SkillLevelData levelData)
        {
            SkillDataSO skill = ownedSkill.skill;
            if (skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(SkillManager)}] Projectile skill has no projectile prefab. skill={skill.name}");
                return false;
            }

            EnemyNetworkBase target = AutoTargeting.FindNearestEnemy(transform.position, levelData.range);
            if (target == null)
            {
                if (Time.time >= nextNoTargetLogTime)
                {
                    Debug.Log($"[{nameof(SkillManager)}] No enemy target in range. skill={skill.name}, range={levelData.range}, position={transform.position}");
                    nextNoTargetLogTime = Time.time + 2f;
                }
                return false;
            }

            Vector3 spawnPosition = projectileSpawnPoint != null
                ? projectileSpawnPoint.position
                : transform.position + Vector3.up * 0.8f;

            Vector3 direction = target.transform.position - spawnPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = transform.forward;

            Vector3 baseDirection = direction.normalized;
            spawnPosition += baseDirection * spawnForwardOffset;

            int projectileCount = Mathf.Max(1, levelData.projectileCount);
            float spreadAngle = Mathf.Max(0f, levelData.spreadAngle);
            int spawnedCount = 0;

            for (int i = 0; i < projectileCount; i++)
            {
                Vector3 shotDirection = GetSpreadDirection(baseDirection, i, projectileCount, spreadAngle);
                if (SpawnProjectile(skill, levelData, spawnPosition, shotDirection))
                    spawnedCount++;
            }

            if (spawnedCount == 0) return false;

            Debug.Log($"[{nameof(SkillManager)}] Fired projectile. skill={skill.name}, level={ownedSkill.level}, target={target.name}, damage={levelData.damage}, count={spawnedCount}, spawn={spawnPosition}");
            return true;
        }

        private bool SpawnProjectile(SkillDataSO skill, SkillLevelData levelData, Vector3 spawnPosition, Vector3 direction)
        {
            Quaternion spawnRotation = Quaternion.LookRotation(direction, Vector3.up);
            if (skill.projectilePrefab.TryGetComponent<NetworkProjectile>(out var projectilePrefab))
                spawnRotation = projectilePrefab.GetProjectileRotation(direction);

            NetworkObject projectileObject = PoolManager.Instance != null
                ? PoolManager.Instance.GetNetworkObject(skill.projectilePrefab, spawnPosition, spawnRotation)
                : Instantiate(skill.projectilePrefab, spawnPosition, spawnRotation).GetComponent<NetworkObject>();

            if (projectileObject == null) return false;
            if (projectileObject.TryGetComponent<NetworkProjectile>(out var projectile))
                projectile.Initialize(skill.projectilePrefab, OwnerClientId, spawnPosition, direction, levelData);
            else
                Debug.LogWarning($"[{nameof(SkillManager)}] Spawned projectile has no {nameof(NetworkProjectile)} component. prefab={skill.projectilePrefab.name}");

            projectileObject.Spawn(true);
            return true;
        }

        private static Vector3 GetSpreadDirection(Vector3 baseDirection, int index, int count, float spreadAngle)
        {
            if (count <= 1 || spreadAngle <= 0f)
                return baseDirection;

            float angleStep = count > 1 ? spreadAngle / (count - 1) : 0f;
            float startAngle = -spreadAngle * 0.5f;
            float angle = startAngle + angleStep * index;
            return Quaternion.AngleAxis(angle, Vector3.up) * baseDirection;
        }

        private bool TryCastAreaAura(OwnedSkill ownedSkill, SkillLevelData levelData)
        {
            SkillDataSO skill = ownedSkill.skill;
            float radius = levelData.areaRadius > 0f ? levelData.areaRadius : levelData.range;
            int targetCount = AutoTargeting.FindEnemiesInRange(transform.position, radius, auraTargets);

            if (targetCount == 0)
            {
                if (Time.time >= nextNoTargetLogTime)
                {
                    Debug.Log($"[{nameof(SkillManager)}] No aura targets in range. skill={skill.name}, radius={radius}, position={transform.position}");
                    nextNoTargetLogTime = Time.time + 2f;
                }
                return false;
            }

            for (int i = 0; i < auraTargets.Count; i++)
                auraTargets[i].TakeDamage(levelData.damage);

            Debug.Log($"[{nameof(SkillManager)}] Aura tick. skill={skill.name}, level={ownedSkill.level}, damage={levelData.damage}, radius={radius}, targets={targetCount}");
            return true;
        }
    }
}
