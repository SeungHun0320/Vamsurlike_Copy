using System;
using System.Collections;
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
            public bool isActive = true;
            public float durationTimer;  // -1 sentinel: 첫 프레임에 levelData.duration으로 초기화
            public float tickTimer;      // 0이면 즉시 첫 틱

            public OwnedSkill(SkillDataSO skill, int level)
            {
                this.skill    = skill;
                this.level    = Mathf.Max(1, level);
                isActive      = true;
                tickTimer     = 0f;
                durationTimer = -1f;
            }
        }

        [SerializeField] private CharacterDataSO characterData;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float spawnForwardOffset = 0.8f;
        [SerializeField] private float failedCastRetryDelay = 0.1f;

        // OrbitalSkill이 생성할 클라이언트 비주얼 설정 — SkillManager Inspector에서 할당
        [SerializeField] private GameObject orbitalVisualPrefab;
        [SerializeField] private float orbitalHeightOffset = 0.9f;

        private PassiveStatHandler passiveStatHandler;
        private PlayerNetworkStats playerStats;

        private readonly List<OwnedSkill> ownedSkills = new();

        // ── Executor registry ─────────────────────────────────────────────────
        private readonly Dictionary<SkillCastType, SkillBase> executorRegistry = new();
        private readonly List<SkillBase> allExecutors = new();
        private OrbitalSkill orbitalSkill;
        // ─────────────────────────────────────────────────────────────────────

        private float nextNoTargetLogTime;

        private void Awake()
        {
            BuildExecutorRegistry();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                // Update()는 비활성화하지 않음 — OrbitalSkill 비주얼 갱신(OnUpdate)이 클라이언트에서도 필요
                Debug.Log($"[{nameof(SkillManager)}] 클라이언트 모드. owner={OwnerClientId}, object={name}");
                return;
            }

            passiveStatHandler = GetComponent<PassiveStatHandler>();
            playerStats        = GetComponent<PlayerNetworkStats>();
            InitializeStartingSkills();
            Debug.Log($"[{nameof(SkillManager)}] 서버 스폰. owner={OwnerClientId}, object={name}, skillCount={ownedSkills.Count}");

            if (ownedSkills.Count == 0)
                Debug.LogWarning($"[{nameof(SkillManager)}] 시작 스킬 없음. object={name}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            for (int i = 0; i < allExecutors.Count; i++)
                allExecutors[i].OnDespawn();
        }

        private void Update()
        {
            // 클라이언트 비주얼 갱신 (OrbitalSkill 등) — 서버/클라이언트 모두 실행
            for (int i = 0; i < allExecutors.Count; i++)
                allExecutors[i].OnUpdate(transform);

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
                        Debug.LogWarning($"[{nameof(SkillManager)}] ownedSkills[{i}].skill is null. object={name}");
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
                if (owned.durationTimer < 0f)
                    owned.durationTimer = levelData.duration;

                owned.tickTimer -= Time.deltaTime;
                if (owned.tickTimer <= 0f)
                {
                    TryCast(owned, levelData);
                    owned.tickTimer = levelData.tickInterval;
                }

                if (levelData.duration > 0f)
                {
                    owned.durationTimer -= Time.deltaTime;
                    if (owned.durationTimer <= 0f)
                    {
                        owned.isActive = false;
                        owned.cooldownTimer = levelData.cooldown;
                        Debug.Log($"[{nameof(SkillManager)}] Persistent 종료. skill={owned.skill.name}, cooldown={levelData.cooldown}s");
                    }
                }
            }
            else
            {
                owned.cooldownTimer -= Time.deltaTime;
                if (owned.cooldownTimer <= 0f)
                {
                    owned.isActive      = true;
                    owned.durationTimer = levelData.duration;
                    owned.tickTimer     = 0f;
                    Debug.Log($"[{nameof(SkillManager)}] Persistent 활성화. skill={owned.skill.name}, duration={levelData.duration}s");
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
            return executorRegistry.TryGetValue(skill.castType, out var executor)
                && executor.IsPersistentExecution;
        }

        public bool LearnSkill(SkillDataSO skill)
        {
            if (!IsServer || skill == null) return false;
            if (TryGetOwnedSkill(skill, out _)) return UpgradeSkill(skill);
            ownedSkills.Add(new OwnedSkill(skill, 1));
            Debug.Log($"[{nameof(SkillManager)}] 스킬 습득. owner={OwnerClientId}, skill={skill.name}");
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
            Debug.Log($"[{nameof(SkillManager)}] 스킬 강화. owner={OwnerClientId}, skill={skill.name}, level={ownedSkill.level}");
            return true;
        }

        // 서버 전용: source 스킬 제거 + evolved 스킬 신규 습득 (진화)
        public bool EvolveSkill(SkillDataSO source, SkillDataSO evolved)
        {
            if (!IsServer || source == null || evolved == null) return false;
            if (!TryGetOwnedSkill(source, out _))
            {
                Debug.LogWarning($"[{nameof(SkillManager)}] EvolveSkill: '{source.name}' 미보유. owner={OwnerClientId}");
                return false;
            }

            for (int i = ownedSkills.Count - 1; i >= 0; i--)
                if (ownedSkills[i].skill == source) { ownedSkills.RemoveAt(i); break; }

            ownedSkills.Add(new OwnedSkill(evolved, 1));
            Debug.Log($"[{nameof(SkillManager)}] 진화. owner={OwnerClientId}, {source.name} → {evolved.name}");
            return true;
        }

        [ServerRpc]
        public void ActivateFirstManualSkillServerRpc()
        {
            if (StageRuntime.Instance == null || StageRuntime.Instance.CurrentState.Value != GameState.Playing) return;

            for (int i = 0; i < ownedSkills.Count; i++)
            {
                OwnedSkill owned = ownedSkills[i];
                if (owned.skill == null || !owned.skill.isManual) continue;
                if (owned.cooldownTimer > 0f)
                {
                    Debug.Log($"[{nameof(SkillManager)}] 수동 스킬 쿨다운 중. skill={owned.skill.name}, remaining={owned.cooldownTimer:F2}s");
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
            return TryGetOwnedSkill(skill, out var owned) ? owned.level : 0;
        }

        // ── Executor를 위한 공용 헬퍼 ─────────────────────────────────────────

        // GrenadeSkill / UltimateSkill 등 코루틴이 필요한 executor가 사용
        public Coroutine StartSkillCoroutine(IEnumerator routine) => StartCoroutine(routine);

        // OrbitalSkill 서버가 호출 — 모든 클라이언트의 OrbitalSkill에 비주얼 생성 지시
        [ClientRpc]
        internal void BroadcastOrbitalClientRpc(int count, float radius, float rotSpeed)
        {
            orbitalSkill?.OnClientOrbitalActivated(count, radius, rotSpeed, transform);
        }

        // UltimateSkill 코루틴이 완료 시 호출 — Phase 8에서 VFX 연결
        [ClientRpc]
        internal void PlayUltimateVFXClientRpc(Vector3 position)
        {
            _ = position; // Phase 8: 궁극기 완료 VFX 연결 시 사용
        }

        // ─────────────────────────────────────────────────────────────────────

        private void InitializeStartingSkills()
        {
            ownedSkills.Clear();
            SkillDataSO[] starting = characterData != null ? characterData.startingSkills : null;
            if (starting == null) return;

            for (int i = 0; i < starting.Length; i++)
            {
                SkillDataSO skill = starting[i];
                if (skill == null)
                {
                    Debug.LogWarning($"[{nameof(SkillManager)}] startingSkills[{i}] is null. object={name}");
                    continue;
                }

                if (TryGetOwnedSkill(skill, out var owned))
                {
                    owned.level = Mathf.Min(owned.level + 1, Mathf.Max(1, skill.maxLevel));
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

            if (!executorRegistry.TryGetValue(skill.castType, out var executor))
            {
                if (Time.time >= nextNoTargetLogTime)
                {
                    Debug.LogWarning($"[{nameof(SkillManager)}] castType={skill.castType}에 대한 executor 없음. skill={skill.name}, object={name}");
                    nextNoTargetLogTime = Time.time + 2f;
                }
                return false;
            }

            float attackMultiplier = passiveStatHandler != null
                ? passiveStatHandler.AttackMultiplier.Value
                : 1f;

            var context = new SkillCastContext(
                this, skill, levelData, ownedSkill.level, OwnerClientId,
                transform, projectileSpawnPoint, spawnForwardOffset, attackMultiplier);

            return executor.TryExecute(context);
        }

        private void BuildExecutorRegistry()
        {
            executorRegistry.Clear();
            allExecutors.Clear();

            orbitalSkill = new OrbitalSkill(orbitalVisualPrefab, orbitalHeightOffset);

            Register(new ProjectileSkill());
            Register(new AuraSkill());
            Register(new MeleeSkill());
            Register(new GrenadeSkill());
            Register(orbitalSkill);
            Register(new ScatterShotSkill());
            Register(new UltimateSkill());
        }

        private void Register(SkillBase executor)
        {
            executorRegistry[executor.SupportedCastType] = executor;
            allExecutors.Add(executor);
        }
    }
}
