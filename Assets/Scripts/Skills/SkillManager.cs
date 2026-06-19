using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Items;
using Vamsurlike.Player;
using Vamsurlike.Stage;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Skills
{
    [RequireComponent(typeof(NetworkObject))]
    public class SkillManager : NetworkBehaviour
    {
        [SerializeField] private CharacterDataSO characterData;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float spawnForwardOffset = 0.8f;
        [SerializeField] private float failedCastRetryDelay = 0.1f;

        // OrbitalSkill 비주얼 — Inspector에서 할당
        [SerializeField] private GameObject orbitalVisualPrefab;
        [SerializeField] private float      orbitalHeightOffset = 0.9f;

        // 스킬 타입별 클라이언트 비주얼 프리팹 — Awake에서 SkillDataSO.vfxPrefab 기반으로 자동 구성
        private readonly Dictionary<SkillCastType, GameObject> vfxPrefabsByType = new();
        private readonly Dictionary<SkillCastType, AreaCircleVFX> areaCircleVfxByType = new();

        private PassiveStatHandler passiveStatHandler;
        private PlayerNetworkStats playerStats;

        private readonly SkillInventory skillInventory = new();
        private readonly SkillExecutionScheduler executionScheduler = new();

        // ── Executor registry ─────────────────────────────────────────────────
        private readonly Dictionary<SkillCastType, SkillBase> executorRegistry = new();
        private readonly List<SkillBase> allExecutors = new();
        private OrbitalSkill        orbitalSkill;
        private AuraSkill           auraSkill;
        private GrenadeSkill        grenadeSkill;
        private OrbitalGrenadeSkill orbitalGrenadeSkill; // 비주얼은 투사체에 귀속 — ClientRpc 미사용
        private BlackHoleSkill      blackHoleSkill;
        // ─────────────────────────────────────────────────────────────────────

        // 클라이언트 HUD 구독용 — 소유자에게 스킬 목록이 동기화될 때 발생
        public static event Action<string[], int[]> OnSkillsSynced;

        private float nextNoTargetLogTime;

        private void Awake()
        {
            BuildExecutorRegistry();
            // VFX 맵은 OnNetworkSpawn()에서 역할 확인 후 구성 (데디케이티드 서버에서는 불필요)
        }

        // SkillDataSO.vfxPrefab 기반으로 타입별 룩업 테이블 구성 — 서버/클라이언트 동일.
        // startingSkills + UpgradeCatalog 전체를 스캔해 나중에 습득하는 스킬도 커버.
        private void BuildVFXPrefabMap()
        {
            // 시작 스킬
            if (characterData != null && characterData.startingSkills != null)
            {
                foreach (var skill in characterData.startingSkills)
                {
                    if (skill != null && skill.vfxPrefab != null)
                        vfxPrefabsByType[skill.castType] = skill.vfxPrefab;
                }
            }

            // 업그레이드 카탈로그에 있는 모든 스킬 (상자·레벨업으로 습득)
            var catalog = UpgradeCatalog.Instance;
            if (catalog != null)
            {
                foreach (var opt in catalog.options)
                {
                    if (opt == null || opt.skillData == null || opt.skillData.vfxPrefab == null) continue;
                    vfxPrefabsByType[opt.skillData.castType] = opt.skillData.vfxPrefab;
                }
            }

            var recipeCatalog = CombineRecipeCatalog.Instance;
            if (recipeCatalog == null || recipeCatalog.recipes == null) return;

            foreach (var recipe in recipeCatalog.recipes)
            {
                if (recipe == null || recipe.evolvedSkill == null || recipe.evolvedSkill.vfxPrefab == null) continue;
                vfxPrefabsByType[recipe.evolvedSkill.castType] = recipe.evolvedSkill.vfxPrefab;
            }
        }

        public override void OnNetworkSpawn()
        {
            // ── 비주얼 레이어: 서버는 VFX 불필요 ─────────────────────────────
            if (!IsServer)
                BuildVFXPrefabMap();

            // ── 클라이언트 초기화 ──────────────────────────────────────────────
            if (!IsServer)
            {
                Debug.Log($"[{nameof(SkillManager)}] 클라이언트 모드. owner={OwnerClientId}, object={name}");
                return;
            }

            // ── 서버 게임 로직 초기화 ──────────────────────────────────────────
            passiveStatHandler = GetComponent<PassiveStatHandler>();
            playerStats        = GetComponent<PlayerNetworkStats>();
            InitializeStartingSkills();
            BroadcastSkillsToOwner();
            Debug.Log($"[{nameof(SkillManager)}] 서버 스폰. owner={OwnerClientId}, object={name}, skillCount={skillInventory.Count}");

            if (skillInventory.Count == 0)
                Debug.LogWarning($"[{nameof(SkillManager)}] 시작 스킬 없음. object={name}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            for (int i = 0; i < allExecutors.Count; i++)
                allExecutors[i].OnDespawn();
            foreach (var entry in areaCircleVfxByType)
                if (entry.Value != null)
                    Destroy(entry.Value.gameObject);
            areaCircleVfxByType.Clear();
        }

        private void Update()
        {
            // ── 클라이언트 비주얼 레이어 ──────────────────────────────────────
            if (!IsServer)
            {
                for (int i = 0; i < allExecutors.Count; i++)
                    allExecutors[i].OnUpdate(transform);
            }

            // ── 서버 게임 로직 레이어 ─────────────────────────────────────────
            if (!IsServer) return;
            if (playerStats != null && !playerStats.IsAlive) return;
            if (StageRuntime.Instance == null || StageRuntime.Instance.CurrentState.Value != GameState.Playing) return;

            executionScheduler.Tick(
                skillInventory.Skills,
                failedCastRetryDelay,
                IsPersistent,
                TryCast,
                message => Debug.LogWarning($"[{nameof(SkillManager)}] {message} object={name}"));
        }

        private bool IsPersistent(SkillDataSO skill)
        {
            return executorRegistry.TryGetValue(skill.castType, out var executor)
                && executor.IsPersistentExecution;
        }

        public bool LearnSkill(SkillDataSO skill)
        {
            if (!IsServer || skill == null) return false;
            if (skillInventory.IsEvolutionLocked(skill))
            {
                Debug.Log($"[{nameof(SkillManager)}] 진화 재료 스킬 재획득 차단. owner={OwnerClientId}, skill={skill.name}");
                return false;
            }

            bool learned = skillInventory.Learn(skill, failedCastRetryDelay, out bool upgradedExisting);
            if (!learned) return false;

            BroadcastSkillsToOwner();
            string action = upgradedExisting ? "스킬 강화" : "스킬 습득";
            Debug.Log($"[{nameof(SkillManager)}] {action}. owner={OwnerClientId}, skill={skill.name}, level={GetSkillLevel(skill)}");
            return true;
        }

        public bool UpgradeSkill(SkillDataSO skill)
        {
            if (!IsServer || skill == null) return false;
            if (skillInventory.IsEvolutionLocked(skill))
            {
                Debug.Log($"[{nameof(SkillManager)}] 진화 재료 스킬 강화/재획득 차단. owner={OwnerClientId}, skill={skill.name}");
                return false;
            }

            bool upgraded = skillInventory.Upgrade(skill, failedCastRetryDelay, out bool learnedMissing);
            if (!upgraded) return false;

            BroadcastSkillsToOwner();
            string action = learnedMissing ? "스킬 습득" : "스킬 강화";
            Debug.Log($"[{nameof(SkillManager)}] {action}. owner={OwnerClientId}, skill={skill.name}, level={GetSkillLevel(skill)}");
            return true;
        }

        // 서버 전용: source 스킬 제거 + evolved 스킬 신규 습득 (진화)
        public bool EvolveSkill(SkillDataSO source, SkillDataSO evolved, SkillDataSO source2 = null)
        {
            if (!IsServer || source == null || evolved == null) return false;

            var removedSkillTypes = new List<SkillCastType>();
            if (!skillInventory.Evolve(source, evolved, source2, removedSkillTypes, out string failureReason))
            {
                Debug.LogWarning($"[{nameof(SkillManager)}] EvolveSkill: {failureReason} owner={OwnerClientId}");
                return false;
            }

            for (int i = 0; i < removedSkillTypes.Count; i++)
                NotifySkillRemoved(removedSkillTypes[i]);

            BroadcastSkillsToOwner();
            string from = source2 != null ? $"{source.name} + {source2.name}" : source.name;
            Debug.Log($"[{nameof(SkillManager)}] 합체/진화. owner={OwnerClientId}, {from} → {evolved.name}");
            return true;
        }

        // 스킬 목록이 바뀔 때마다 소유 클라이언트에 현재 상태 전송
        // NGO RPC는 string[]을 지원하지 않으므로 이름을 '|' 구분자로 합쳐서 전송
        private void BroadcastSkillsToOwner()
        {
            skillInventory.BuildSnapshot(out string joinedNames, out int[] levels);
            SyncSkillsToOwnerClientRpc(joinedNames, levels, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            });
        }

        private void NotifySkillRemoved(SkillCastType castType)
        {
            if (executorRegistry.TryGetValue(castType, out var executor))
                executor.OnSkillRemoved(castType);

            NotifySkillRemovedClientRpc(castType, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            });
        }

        [ClientRpc]
        private void NotifySkillRemovedClientRpc(SkillCastType castType, ClientRpcParams rpcParams = default)
        {
            if (executorRegistry.TryGetValue(castType, out var executor))
                executor.OnSkillRemoved(castType);
            DestroyAreaCircleVFX(castType);
        }

        [ClientRpc]
        private void SyncSkillsToOwnerClientRpc(string joinedNames, int[] levels, ClientRpcParams rpcParams = default)
        {
            string[] names = joinedNames.Length > 0
                ? joinedNames.Split('|')
                : System.Array.Empty<string>();
            OnSkillsSynced?.Invoke(names, levels);
        }

        [ServerRpc]
        public void ActivateFirstManualSkillServerRpc()
        {
            if (StageRuntime.Instance == null || StageRuntime.Instance.CurrentState.Value != GameState.Playing) return;

            IReadOnlyList<SkillRuntimeState> skills = skillInventory.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillRuntimeState owned = skills[i];
                if (owned.Skill == null || !owned.Skill.isManual) continue;
                if (owned.CooldownTimer > 0f)
                {
                    Debug.Log($"[{nameof(SkillManager)}] 수동 스킬 쿨다운 중. skill={owned.Skill.name}, remaining={owned.CooldownTimer:F2}s");
                    return;
                }

                SkillLevelData levelData = owned.Skill.GetLevelData(owned.Level);
                owned.CooldownTimer = TryCast(owned, levelData)
                    ? levelData != null ? levelData.cooldown : 5f
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

        // ── Executor를 위한 공용 헬퍼 ─────────────────────────────────────────

        // GrenadeSkill / UltimateSkill 등 코루틴이 필요한 executor가 사용
        public Coroutine StartSkillCoroutine(IEnumerator routine) => StartCoroutine(routine);

        // OrbitalSkill 서버가 호출 — 모든 클라이언트의 OrbitalSkill에 비주얼 생성 지시
        [ClientRpc]
        internal void BroadcastOrbitalClientRpc(int count, float radius, float rotSpeed)
        {
            orbitalSkill?.OnClientOrbitalActivated(count, radius, rotSpeed, transform);
        }

        // OrbitalShot: 위성 비주얼은 투사체에 귀속 — 별도 ClientRpc 불필요 (Phase N에서 VFX 연결)
        [ClientRpc]
        internal void BroadcastOrbitalGrenadeClientRpc(int count, float radius, float rotSpeed)
        {
            _ = count; _ = radius; _ = rotSpeed;
        }

        // BlackHole 위치 기반 VFX — Phase N에서 이펙트 연결
        [ClientRpc]
        internal void BroadcastBlackHolePosClientRpc(Vector3 center, float duration, float radius)
        {
            SpawnAreaCircleVFX(SkillCastType.BlackHole, center, radius, duration, new Color(0.45f, 0.1f, 1f, 0.9f));
        }

        [ClientRpc]
        internal void BroadcastAreaCircleClientRpc(SkillCastType castType, float radius, float duration, bool followOwner)
        {
            Transform followTarget = followOwner ? transform : null;
            Vector3 position = followTarget != null ? followTarget.position : transform.position;
            Color color = castType == SkillCastType.AreaAura
                ? new Color(0.15f, 0.85f, 1f, 0.75f)
                : new Color(0.45f, 0.1f, 1f, 0.9f);

            SpawnAreaCircleVFX(castType, position, radius, duration, color, followTarget);
        }

        // UltimateSkill 코루틴이 완료 시 호출 — Phase 8에서 VFX 연결
        [ClientRpc]
        internal void PlayUltimateVFXClientRpc(Vector3 position)
        {
            _ = position; // Phase 8: 궁극기 완료 VFX 연결 시 사용
        }

        // MeleeSkill: 타격 시 클라이언트에 모델 순간 생성 (0.5s)
        [ClientRpc]
        internal void BroadcastMeleeVFXClientRpc(Vector3 position, Vector3 forward)
        {
            if (!vfxPrefabsByType.TryGetValue(SkillCastType.Melee, out var prefab) || prefab == null) return;
            var go = Instantiate(prefab, position, Quaternion.LookRotation(forward, Vector3.up));
            Destroy(go, 0.5f);
        }

        // GrenadeSkill: 포물선 모델 비행
        [ClientRpc]
        internal void BroadcastGrenadeVFXClientRpc(Vector3 from, Vector3 to, float arcHeight, float flightTime)
        {
            if (!vfxPrefabsByType.TryGetValue(SkillCastType.Grenade, out var prefab)) prefab = null;
            StartCoroutine(GrenadeVisualCoroutine(from, to, arcHeight, flightTime, prefab));
        }

        private System.Collections.IEnumerator GrenadeVisualCoroutine(
            Vector3 from, Vector3 to, float arcHeight, float flightTime, GameObject prefab)
        {
            var visual = prefab != null ? Instantiate(prefab, from, Quaternion.identity) : null;

            float elapsed = 0f;
            while (elapsed < flightTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flightTime);
                Vector3 pos = Vector3.Lerp(from, to, t);
                pos.y += arcHeight * 4f * t * (1f - t);
                if (visual != null) visual.transform.position = pos;
                yield return null;
            }

            if (visual != null) Destroy(visual);
        }

        // ─────────────────────────────────────────────────────────────────────

        private void InitializeStartingSkills()
        {
            SkillDataSO[] starting = characterData != null ? characterData.startingSkills : null;
            skillInventory.InitializeStartingSkills(
                starting,
                message => Debug.LogWarning($"[{nameof(SkillManager)}] {message} object={name}"));
        }

        private bool TryCast(SkillRuntimeState ownedSkill, SkillLevelData levelData)
        {
            SkillDataSO skill = ownedSkill.Skill;
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

            float baseSpeed    = characterData != null ? characterData.baseMoveSpeed : 5f;
            float currentSpeed = playerStats   != null ? playerStats.MoveSpeed.Value : baseSpeed;
            float speedMultiplier = baseSpeed > 0f ? currentSpeed / baseSpeed : 1f;

            var context = new SkillCastContext(
                this, skill, levelData, ownedSkill.Level, OwnerClientId,
                transform, projectileSpawnPoint, spawnForwardOffset, attackMultiplier, speedMultiplier);

            return executor.TryExecute(context);
        }

        private void BuildExecutorRegistry()
        {
            executorRegistry.Clear();
            allExecutors.Clear();

            orbitalSkill        = new OrbitalSkill(orbitalVisualPrefab, orbitalHeightOffset);
            auraSkill           = new AuraSkill();
            grenadeSkill        = new GrenadeSkill();
            orbitalGrenadeSkill = new OrbitalGrenadeSkill();
            blackHoleSkill      = new BlackHoleSkill();

            Register(new ProjectileSkill());
            Register(auraSkill);
            Register(new MeleeSkill());
            Register(grenadeSkill);
            Register(orbitalSkill);
            Register(new ScatterShotSkill());
            Register(new ClusterGrenadeSkill());
            Register(new UltimateSkill());
            Register(orbitalGrenadeSkill);
            Register(blackHoleSkill);
            Register(new PierceShotgunSkill());
        }

        private void Register(SkillBase executor)
        {
            executorRegistry[executor.SupportedCastType] = executor;
            allExecutors.Add(executor);
        }

        private void SpawnAreaCircleVFX(
            SkillCastType castType,
            Vector3 position,
            float radius,
            float duration,
            Color color,
            Transform followTarget = null)
        {
            if (!vfxPrefabsByType.TryGetValue(castType, out var prefab) || prefab == null) return;

            if (duration <= 0f)
                DestroyAreaCircleVFX(castType);

            GameObject go = Instantiate(prefab, position, Quaternion.identity);
            if (!go.TryGetComponent<AreaCircleVFX>(out var vfx))
            {
                Debug.LogWarning($"[{nameof(SkillManager)}] AreaCircleVFX 컴포넌트 없음. prefab={prefab.name}");
                Destroy(go);
                return;
            }

            vfx.Initialize(radius, duration, color, followTarget);

            if (duration <= 0f)
                areaCircleVfxByType[castType] = vfx;
        }

        private void DestroyAreaCircleVFX(SkillCastType castType)
        {
            if (!areaCircleVfxByType.TryGetValue(castType, out var vfx)) return;
            if (vfx != null)
                Destroy(vfx.gameObject);
            areaCircleVfxByType.Remove(castType);
        }
    }
}
