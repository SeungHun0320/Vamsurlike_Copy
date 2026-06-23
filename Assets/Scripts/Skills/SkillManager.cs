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
    [RequireComponent(typeof(NetworkObject), typeof(SkillVFXController), typeof(PlayerNetworkController))]
    public class SkillManager : NetworkBehaviour, ISkillCoroutineRunner
    {
        private const float DefaultManualCooldown = 5f;
        private const float MissingExecutorLogInterval = 2f;

        [SerializeField] private CharacterDataSO characterData;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float spawnForwardOffset = 0.8f;
        [SerializeField] private float failedCastRetryDelay = 0.1f;

        private PassiveStatHandler      passiveStatHandler;
        private PlayerNetworkStats      playerStats;
        private PlayerNetworkController controller;
        private SkillVFXController      skillVfxController;

        private readonly SkillInventory skillInventory = new();
        private readonly SkillExecutionScheduler executionScheduler = new();
        private readonly Dictionary<SkillCastType, SkillBase> executorRegistry = new();
        private readonly List<SkillBase> allExecutors = new();

        private float nextMissingExecutorLogTime;

        public static event Action<string[], int[]> OnSkillsSynced;

        private void Awake()
        {
            passiveStatHandler = GetComponent<PassiveStatHandler>();
            playerStats        = GetComponent<PlayerNetworkStats>();
            controller         = GetComponent<PlayerNetworkController>();
            skillVfxController = GetComponent<SkillVFXController>();

            if (characterData == null)
                Debug.LogError($"[{nameof(SkillManager)}] characterData가 없습니다.", this);
            if (projectileSpawnPoint == null)
                Debug.LogWarning($"[{nameof(SkillManager)}] projectileSpawnPoint가 없어 플레이어 위치를 사용합니다.", this);
            if (passiveStatHandler == null)
                Debug.LogError($"[{nameof(SkillManager)}] PassiveStatHandler가 없습니다.", this);
            if (playerStats == null)
                Debug.LogError($"[{nameof(SkillManager)}] PlayerNetworkStats가 없습니다.", this);
            if (skillVfxController == null)
                Debug.LogError($"[{nameof(SkillManager)}] SkillVFXController가 없습니다.", this);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            BuildExecutorRegistry();
            InitializeStartingSkills();
            BroadcastSkillsToOwner();

            if (skillInventory.Count == 0)
                Debug.LogWarning($"[{nameof(SkillManager)}] 시작 스킬이 없습니다. object={name}");
        }

        public override void OnNetworkDespawn()
        {
            for (int i = 0; i < allExecutors.Count; i++)
                allExecutors[i].OnDespawn();
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer) return;
            if (playerStats != null && !playerStats.CanAct) return;
            if (StageRuntime.Instance == null
                || StageRuntime.Instance.CurrentState.Value != GameState.Playing)
                return;

            executionScheduler.Tick(
                skillInventory.Skills,
                failedCastRetryDelay,
                IsPersistent,
                TryCast,
                message => Debug.LogWarning(
                    $"[{nameof(SkillManager)}] {message} object={name}"));
        }

        public bool LearnSkill(SkillDataSO skill)
        {
            if (!IsServer || skill == null) return false;
            if (skillInventory.IsEvolutionLocked(skill))
                return false;

            if (!skillInventory.Learn(skill, failedCastRetryDelay, out _)) return false;

            BroadcastSkillsToOwner();
            return true;
        }

        public bool UpgradeSkill(SkillDataSO skill)
        {
            if (!IsServer || skill == null) return false;
            if (skillInventory.IsEvolutionLocked(skill))
            {
                Debug.Log(
                    $"[{nameof(SkillManager)}] 진화 재료 스킬 강화/재획득 차단. " +
                    $"owner={OwnerClientId}, skill={skill.name}");
                return false;
            }

            if (!skillInventory.Upgrade(skill, failedCastRetryDelay, out _)) return false;

            BroadcastSkillsToOwner();
            return true;
        }

        public bool EvolveSkill(
            SkillDataSO source,
            SkillDataSO evolved,
            SkillDataSO source2 = null)
        {
            if (!IsServer || source == null || evolved == null) return false;

            var removedSkillTypes = new List<SkillCastType>();
            if (!skillInventory.Evolve(
                    source,
                    evolved,
                    source2,
                    removedSkillTypes,
                    out string failureReason))
            {
                Debug.LogWarning(
                    $"[{nameof(SkillManager)}] EvolveSkill: {failureReason} " +
                    $"owner={OwnerClientId}");
                return false;
            }

            for (int i = 0; i < removedSkillTypes.Count; i++)
                NotifySkillRemoved(removedSkillTypes[i]);

            BroadcastSkillsToOwner();
            return true;
        }

        [ServerRpc]
        public void ActivateFirstManualSkillServerRpc()
        {
            if (!IsServer) return;
            if (StageRuntime.Instance == null
                || StageRuntime.Instance.CurrentState.Value != GameState.Playing)
            {
                Debug.LogWarning($"[ActivateFirstManualSkillServerRpc] FAIL — GameState이 Playing이 아님.");
                return;
            }

            IReadOnlyList<SkillRuntimeState> skills = skillInventory.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillRuntimeState owned = skills[i];
                if (owned.Skill == null || !owned.Skill.isManual) continue;
                if (owned.CooldownTimer > 0f) return;

                SkillLevelData levelData = owned.Skill.GetLevelData(owned.Level);
                owned.CooldownTimer = TryCast(owned, levelData)
                    ? levelData != null ? levelData.cooldown : DefaultManualCooldown
                    : failedCastRetryDelay;
                return;
            }
        }

        public int GetSkillLevel(SkillDataSO skill)
        {
            return skillInventory.GetSkillLevel(skill);
        }

        public bool IsEvolutionLocked(SkillDataSO skill)
        {
            return skillInventory.IsEvolutionLocked(skill);
        }

        public Coroutine StartSkillCoroutine(IEnumerator routine)
        {
            if (!IsServer || routine == null) return null;
            return StartCoroutine(routine);
        }

        private bool IsPersistent(SkillDataSO skill)
        {
            return skill != null
                && executorRegistry.TryGetValue(skill.castType, out SkillBase executor)
                && executor.IsPersistentExecution;
        }

        private void InitializeStartingSkills()
        {
            SkillDataSO[] starting = characterData != null
                ? characterData.startingSkills
                : null;
            skillInventory.InitializeStartingSkills(
                starting,
                message => Debug.LogWarning(
                    $"[{nameof(SkillManager)}] {message} object={name}"));
        }

        private bool TryCast(SkillRuntimeState ownedSkill, SkillLevelData levelData)
        {
            SkillDataSO skill = ownedSkill.Skill;
            if (skill == null || levelData == null) return false;

            if (!executorRegistry.TryGetValue(skill.castType, out SkillBase executor))
            {
                if (Time.time >= nextMissingExecutorLogTime)
                {
                    Debug.LogWarning(
                        $"[{nameof(SkillManager)}] castType={skill.castType} executor가 없습니다. " +
                        $"skill={skill.name}, object={name}");
                    nextMissingExecutorLogTime = Time.time + MissingExecutorLogInterval;
                }
                return false;
            }

            float attackMultiplier = passiveStatHandler != null
                ? passiveStatHandler.AttackMultiplier.Value
                : 1f;
            float baseSpeed = characterData != null ? characterData.baseMoveSpeed : 1f;
            float currentSpeed = playerStats != null
                ? playerStats.MoveSpeed.Value
                : baseSpeed;
            float speedMultiplier = baseSpeed > 0f
                ? currentSpeed / baseSpeed
                : 1f;

            Vector3 casterForward = controller != null
                ? controller.LastNonZeroMoveDirection
                : transform.forward;

            var context = new SkillCastContext(
                this,
                skillVfxController,
                skill,
                levelData,
                ownedSkill.Level,
                OwnerClientId,
                transform,
                projectileSpawnPoint,
                spawnForwardOffset,
                casterForward,
                attackMultiplier,
                speedMultiplier);

            return executor.TryExecute(context);
        }

        private void BroadcastSkillsToOwner()
        {
            skillInventory.BuildSnapshot(out string joinedNames, out int[] levels);
            SyncSkillsToOwnerClientRpc(joinedNames, levels, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            });
        }

        private void NotifySkillRemoved(SkillCastType castType)
        {
            if (executorRegistry.TryGetValue(castType, out SkillBase executor))
                executor.OnSkillRemoved(castType);
            skillVfxController?.RemoveSkillVisual(castType);
        }

        [ClientRpc]
        private void SyncSkillsToOwnerClientRpc(
            string joinedNames,
            int[] levels,
            ClientRpcParams rpcParams = default)
        {
            string[] names = joinedNames.Length > 0
                ? joinedNames.Split('|')
                : Array.Empty<string>();
            OnSkillsSynced?.Invoke(names, levels);
        }

        private void BuildExecutorRegistry()
        {
            executorRegistry.Clear();
            allExecutors.Clear();

            Register(new ProjectileSkill());
            Register(new AuraSkill());
            Register(new MeleeSkill());
            Register(new GrenadeSkill());
            Register(new OrbitalSkill());
            Register(new ScatterShotSkill());
            Register(new ClusterGrenadeSkill());
            Register(new UltimateSkill());
            Register(new OrbitalGrenadeSkill());
            Register(new BlackHoleSkill());
            Register(new PierceShotgunSkill());
        }

        private void Register(SkillBase executor)
        {
            if (executor == null) return;
            executorRegistry[executor.SupportedCastType] = executor;
            allExecutors.Add(executor);
        }
    }
}
