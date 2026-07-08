using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Audio;
using Vamsurlike.Data;
using Vamsurlike.Player;
using Vamsurlike.Stage;
using Vamsurlike.UI.Events;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Skills
{
    public struct SkillSlotSyncData : INetworkSerializable
    {
        public FixedString64Bytes skillName;
        public int                level;
        public int                maxLevel;

        public SkillSlotSyncData(string skillName, int level, int maxLevel)
        {
            this.skillName = default;
            this.skillName.CopyFromTruncated(string.IsNullOrWhiteSpace(skillName) ? "?" : skillName);
            this.level    = level;
            this.maxLevel = Mathf.Max(1, maxLevel);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref skillName);
            serializer.SerializeValue(ref level);
            serializer.SerializeValue(ref maxLevel);
        }
    }

    [RequireComponent(typeof(NetworkObject), typeof(SkillVFXController), typeof(PlayerNetworkController))]
    [RequireComponent(typeof(PlayerMatchStats))]
    public class SkillManager : NetworkBehaviour, ISkillCoroutineRunner
    {
        private const float DefaultManualCooldown = 5f;
        private const float MissingExecutorLogInterval = 2f;

        [SerializeField] private CharacterDataSO characterData;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float spawnForwardOffset = 0.8f;
        [SerializeField] private float failedCastRetryDelay = 0.1f;
        [SerializeField] private SFXSpawnEventSO sfxSpawnEvent;


        private PassiveStatHandler      passiveStatHandler;
        private PlayerNetworkStats      playerStats;
        private PlayerNetworkController controller;
        private SkillVFXController      skillVfxController;

        private readonly SkillInventory skillInventory = new();
        private readonly SkillExecutionScheduler executionScheduler = new();
        private readonly Dictionary<SkillCastType, SkillBase> executorRegistry = new();
        private readonly List<SkillBase> allExecutors = new();

        private float nextMissingExecutorLogTime;

        // Owner 전용 수동 스킬 쿨다운 동기화 (UI 표시용, ManualSkillAdapter가 읽음)
        public readonly NetworkVariable<float> ManualCooldownRemaining = new(
            0f, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<float> ManualCooldownDuration = new(
            DefaultManualCooldown, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);


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
            if (GameFlowCoordinator.Instance == null
                || !GameFlowCoordinator.Instance.IsGameplayActive)
                return;

            float durationMultiplier = passiveStatHandler != null
                ? passiveStatHandler.DurationMultiplier.Value
                : 1f;
            // §7.5 스킬 가속 공식: 쿨다운 = 기본 쿨다운 / (1 + 가속/100)
            float cooldownMultiplier = GetCooldownMultiplier();

            executionScheduler.Tick(
                skillInventory.Skills,
                failedCastRetryDelay,
                IsPersistent,
                TryCast,
                owned => NotifySkillRemoved(owned.Skill.castType),
                message => Debug.LogWarning(
                    $"[{nameof(SkillManager)}] {message} object={name}"),
                durationMultiplier,
                cooldownMultiplier);

            SyncManualCooldown();
        }

        private float GetCooldownMultiplier()
        {
            return passiveStatHandler != null
                ? 1f / (1f + passiveStatHandler.SkillHaste.Value / 100f)
                : 1f;
        }

        private void SyncManualCooldown()
        {
            IReadOnlyList<SkillRuntimeState> skills = skillInventory.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillRuntimeState owned = skills[i];
                if (owned.Skill == null || !owned.Skill.isManual) continue;

                SkillLevelData levelData = owned.Skill.GetLevelData(owned.Level);
                float duration = levelData != null
                    ? levelData.cooldown * GetCooldownMultiplier()
                    : DefaultManualCooldown;

                ManualCooldownRemaining.Value = Mathf.Max(0f, owned.CooldownTimer);
                ManualCooldownDuration.Value = Mathf.Max(0.01f, duration);
                return;
            }

            ManualCooldownRemaining.Value = 0f;
            ManualCooldownDuration.Value = DefaultManualCooldown;
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
            SkillDataSO evolved)
        {
            if (!IsServer || source == null || evolved == null) return false;

            var removedSkillTypes = new List<SkillCastType>();
            if (!skillInventory.Evolve(
                    source,
                    evolved,
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
            if (GameFlowCoordinator.Instance == null
                || !GameFlowCoordinator.Instance.IsGameplayActive)
            {
                Debug.LogWarning($"[ActivateFirstManualSkillServerRpc] FAIL - gameplay is not active.");
                return;
            }

            IReadOnlyList<SkillRuntimeState> skills = skillInventory.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillRuntimeState owned = skills[i];
                if (owned.Skill == null || !owned.Skill.isManual) continue;

                if (owned.CooldownTimer > 0f)
                {
                    Debug.LogWarning($"[ActivateFirstManualSkillServerRpc] FAIL - cooldown remaining={owned.CooldownTimer:F1}, owner={OwnerClientId}");
                    return;
                }

                SkillLevelData levelData = owned.Skill.GetLevelData(owned.Level);
                if (TryCast(owned, levelData))
                {
                    owned.CooldownTimer = levelData != null
                        ? levelData.cooldown * GetCooldownMultiplier()
                        : DefaultManualCooldown;
                }
                else
                {
                    owned.CooldownTimer = failedCastRetryDelay;
                }
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
            float areaMultiplier = passiveStatHandler != null
                ? passiveStatHandler.AreaMultiplier.Value
                : 1f;
            int bonusProjectileCount = passiveStatHandler != null
                ? passiveStatHandler.BonusProjectileCount
                : 0;
            float durationMultiplier = passiveStatHandler != null
                ? passiveStatHandler.DurationMultiplier.Value
                : 1f;
            float skillHasteValue = passiveStatHandler != null
                ? passiveStatHandler.SkillHaste.Value
                : 0f;
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
                speedMultiplier,
                areaMultiplier,
                bonusProjectileCount,
                durationMultiplier,
                skillHasteValue);

            bool casted = executor.TryExecute(context);
            // 지속형 스킬(오라/오비탈)은 틱마다 TryCast가 재호출되므로 캐스트 SFX 대상에서 제외 —
            // 발동 순간 한 번만 울려야 하는 일반 스킬에만 적용한다.
            if (casted && !executor.IsPersistentExecution)
                PlaySkillCastSfxClientRpc(transform.position);

            return casted;
        }

        [ClientRpc]
        private void PlaySkillCastSfxClientRpc(Vector3 position)
        {
            sfxSpawnEvent?.Raise(new SFXCue(SFXCueIds.SkillCast, position));
        }

        private void BroadcastSkillsToOwner()
        {
            SkillSlotSyncData[] slots = skillInventory.BuildSnapshot();
            SyncSkillsToOwnerClientRpc(slots, new ClientRpcParams
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
            SkillSlotSyncData[] slots,
            ClientRpcParams rpcParams = default)
        {
            int count = slots != null ? slots.Length : 0;
            string[] names     = count > 0 ? new string[count] : Array.Empty<string>();
            int[]    levels    = count > 0 ? new int[count]    : Array.Empty<int>();
            int[]    maxLevels = count > 0 ? new int[count]    : Array.Empty<int>();

            for (int i = 0; i < count; i++)
            {
                names[i]     = slots[i].skillName.ToString();
                levels[i]    = slots[i].level;
                maxLevels[i] = slots[i].maxLevel;
            }

            OnSkillsSynced?.Invoke(names, levels); // 임시 경유지 (Phase 8 마이그레이션 완료 후 제거)
            UIEventHub.Instance?.Skill.PublishSkillSlotsChanged(new SkillSlotsPayload(names, levels, maxLevels));
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
            Register(new EarthshatterSkill());
            Register(new GaleSpreadSkill());
            Register(new ShotgunSkill());
            Register(new PiercingBoomerangSkill());
            Register(new LifeDrainBoltSkill());
        }

        private void Register(SkillBase executor)
        {
            if (executor == null) return;
            executorRegistry[executor.SupportedCastType] = executor;
            allExecutors.Add(executor);
        }
    }
}
