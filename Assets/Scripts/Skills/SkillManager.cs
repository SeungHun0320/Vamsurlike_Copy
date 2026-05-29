using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Player;
using Vamsurlike.Stage;
using Vamsurlike.Upgrades;

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

            // Projectile / Ultimate
            public float cooldownTimer;

            // Aura / Orbital (persistent)
            public bool isActive = true;   // 활성 상태 (duration=0이면 항상 true)
            public float durationTimer;    // 남은 지속시간
            public float tickTimer;        // 다음 틱까지 남은 시간

            public OwnedSkill(SkillDataSO skill, int level)
            {
                this.skill    = skill;
                this.level    = Mathf.Max(1, level);
                isActive      = true;
                tickTimer     = 0f;   // 즉시 첫 틱
                durationTimer = -1f;  // sentinel: 첫 UpdatePersistentSkill에서 levelData.duration으로 초기화
            }
        }

        [SerializeField] private CharacterDataSO characterData;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float spawnForwardOffset = 0.8f;
        [SerializeField] private float failedCastRetryDelay = 0.1f;

        private PassiveStatHandler  passiveStatHandler;
        private PlayerNetworkStats  playerStats;

        private readonly List<OwnedSkill> ownedSkills = new();
        private readonly List<SkillBase> skillExecutors = new();
        private float nextNoTargetLogTime;

        private void Awake()
        {
            CacheSkillExecutors();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                Debug.Log($"[{nameof(SkillManager)}] Disabled on client. owner={OwnerClientId}, object={name}");
                enabled = false;
                return;
            }

            passiveStatHandler = GetComponent<PassiveStatHandler>();
            playerStats        = GetComponent<PlayerNetworkStats>();
            InitializeStartingSkills();
            CacheSkillExecutors();
            Debug.Log($"[{nameof(SkillManager)}] Spawned on server. owner={OwnerClientId}, object={name}, skillCount={ownedSkills.Count}");

            if (ownedSkills.Count == 0)
                Debug.LogWarning($"[{nameof(SkillManager)}] No starting skills assigned. object={name}");

            if (skillExecutors.Count == 0)
                Debug.LogWarning($"[{nameof(SkillManager)}] No skill executors found. Add {nameof(ProjectileNetworkSkill)}, {nameof(AuraNetworkSkill)}, {nameof(OrbitalNetworkSkill)}, or another {nameof(SkillBase)} component. object={name}");
        }

        private void Update()
        {
            if (!IsServer) return;
            if (playerStats != null && !playerStats.IsAlive) return;
            if (StageRuntime.Instance == null || StageRuntime.Instance.CurrentState.Value != GameState.Playing) return;

            for (int i = 0; i < ownedSkills.Count; i++)
            {
                OwnedSkill owned = ownedSkills[i];
                if (owned.skill == null)
                {
                    if (Time.time >= nextNoTargetLogTime)
                    {
                        Debug.LogWarning($"[{nameof(SkillManager)}] ownedSkills[{i}] is null. object={name}");
                        nextNoTargetLogTime = Time.time + 2f;
                    }
                    continue;
                }

                if (IsPersistent(owned.skill))
                    UpdatePersistentSkill(owned);
                else
                    UpdateCooldownSkill(owned);
            }
        }

        // Aura / Orbital: tickInterval마다 데미지, duration 후 cooldown 대기
        private void UpdatePersistentSkill(OwnedSkill owned)
        {
            SkillLevelData levelData = owned.skill.GetLevelData(owned.level);
            if (levelData == null) return;

            if (owned.isActive)
            {
                // sentinel: 첫 프레임에 duration 초기화
                if (owned.durationTimer < 0f)
                    owned.durationTimer = levelData.duration;

                owned.tickTimer -= Time.deltaTime;
                if (owned.tickTimer <= 0f)
                {
                    TryCast(owned, levelData);
                    owned.tickTimer = levelData.tickInterval;
                }

                // duration=0 이면 항상 활성
                if (levelData.duration > 0f)
                {
                    owned.durationTimer -= Time.deltaTime;
                    if (owned.durationTimer <= 0f)
                    {
                        owned.isActive = false;
                        owned.cooldownTimer = levelData.cooldown;
                        Debug.Log($"[{nameof(SkillManager)}] Persistent skill ended. skill={owned.skill.name}, cooldown={levelData.cooldown}s");
                    }
                }
            }
            else
            {
                owned.cooldownTimer -= Time.deltaTime;
                if (owned.cooldownTimer <= 0f)
                {
                    owned.isActive     = true;
                    owned.durationTimer = levelData.duration;
                    owned.tickTimer    = 0f;
                    Debug.Log($"[{nameof(SkillManager)}] Persistent skill activated. skill={owned.skill.name}, duration={levelData.duration}s");
                }
            }
        }

        // Projectile / Ultimate: cooldown 후 발동
        private void UpdateCooldownSkill(OwnedSkill owned)
        {
            owned.cooldownTimer -= Time.deltaTime;
            if (owned.skill.isManual) return;
            if (owned.cooldownTimer > 0f) return;

            SkillLevelData levelData = owned.skill.GetLevelData(owned.level);
            owned.cooldownTimer = TryCast(owned, levelData)
                ? levelData != null ? levelData.cooldown : 1f
                : failedCastRetryDelay;
        }

        private bool IsPersistent(SkillDataSO skill)
        {
            SkillBase executor = FindExecutor(skill);
            return executor != null && executor.IsPersistentExecution;
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

        [ServerRpc]
        public void ActivateFirstManualSkillServerRpc()
        {
            if(StageRuntime.Instance?.CurrentState.Value != GameState.Playing)
                return;

            for (int i = 0; i < ownedSkills.Count; i++)
            {
                OwnedSkill owned = ownedSkills[i];
                if (owned.skill == null || !owned.skill.isManual) continue;
                if (owned.cooldownTimer > 0f)
                {
                    Debug.Log($"[{nameof(SkillManager)}] Manual skill on cooldown. skill={owned.skill.name}, remaining={owned.cooldownTimer:F2}s");
                    return;
                }

                SkillLevelData levelData = owned.skill.GetLevelData(owned.level);
                owned.cooldownTimer = TryCast(owned, levelData)
                    ? levelData != null ? levelData.cooldown : 5f
                    : failedCastRetryDelay;
                return;
            }
        }

        public int GetSkillLevel(SkillDataSO skill)
        {
            return TryGetOwnedSkill(skill, out var ownedSkill) ? ownedSkill.level : 0;
        }

        private void InitializeStartingSkills()
        {
            ownedSkills.Clear();
            SkillDataSO[] startingSkills = characterData != null ? characterData.startingSkills : null;
            if (startingSkills == null) return;

            for (int i = 0; i < startingSkills.Length; i++)
            {
                SkillDataSO skill = startingSkills[i];
                if (skill == null)
                {
                    Debug.LogWarning($"[{nameof(SkillManager)}] characterData.startingSkills[{i}] is null. object={name}");
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

            SkillBase executor = FindExecutor(skill);
            if (executor == null)
            {
                if (Time.time >= nextNoTargetLogTime)
                {
                    Debug.LogWarning($"[{nameof(SkillManager)}] No executor found for skill. skill={skill.name}, castType={skill.castType}, object={name}");
                    nextNoTargetLogTime = Time.time + 2f;
                }
                return false;
            }

            float attackMultiplier = passiveStatHandler != null
                ? passiveStatHandler.AttackMultiplier.Value
                : 1f;

            var context = new SkillCastContext(
                this,
                skill,
                levelData,
                ownedSkill.level,
                OwnerClientId,
                transform,
                projectileSpawnPoint,
                spawnForwardOffset,
                attackMultiplier);

            return executor.TryExecute(context);
        }

        private void CacheSkillExecutors()
        {
            skillExecutors.Clear();
            GetComponents(skillExecutors);
        }

        private SkillBase FindExecutor(SkillDataSO skill)
        {
            if (skillExecutors.Count == 0)
                CacheSkillExecutors();

            for (int i = 0; i < skillExecutors.Count; i++)
            {
                SkillBase executor = skillExecutors[i];
                if (executor != null && executor.CanExecute(skill))
                    return executor;
            }

            return null;
        }
    }
}
