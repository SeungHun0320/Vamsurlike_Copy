using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Items;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Stage
{
    // Stage 씬의 NetworkObject로 배치 (LevelUpManager와 같은 오브젝트).
    // XP/Level은 플레이어별이 아닌 게임 전체 공유.
    [RequireComponent(typeof(LevelUpManager))]
    public class SharedLevelSystem : NetworkBehaviour
    {
        public static SharedLevelSystem Instance { get; private set; }

        public NetworkVariable<float> SharedXP { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> SharedLevel { get; } = new(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        [Header("Debug")]
        [SerializeField] private bool enableDebugLevelUpKey       = true;
        [SerializeField] private Key  debugLevelUpKey             = Key.L;
        [SerializeField] private bool enableDebugKillVisibleKey   = true;
        [SerializeField] private Key  debugKillVisibleEnemiesKey  = Key.K;
        [SerializeField] private bool enableDebugSpawnItemKey     = true;
        [SerializeField] private Key  debugSpawnItemKey           = Key.I;
        [SerializeField] private ItemDataSO[] debugSpawnItems;

        private LevelUpManager levelUpManager;
        private readonly List<ulong> visibleEnemyIds = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
            levelUpManager = GetComponent<LevelUpManager>();
            if (levelUpManager == null)
                Debug.LogError($"[{nameof(SharedLevelSystem)}] LevelUpManager 컴포넌트를 찾을 수 없습니다.", this);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsSpawned) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (enableDebugLevelUpKey && keyboard[debugLevelUpKey].wasPressedThisFrame)
            {
                if (IsServer)
                    GrantDebugLevelUpXP();
                else
                    RequestDebugLevelUpRpc();
            }

            if (enableDebugKillVisibleKey && keyboard[debugKillVisibleEnemiesKey].wasPressedThisFrame)
                KillVisibleEnemiesByDebugKey();

            if (enableDebugSpawnItemKey && keyboard[debugSpawnItemKey].wasPressedThisFrame)
            {
                if (IsServer)
                    SpawnDebugItems();
                else
                    RequestSpawnDebugItemsRpc();
            }
#endif
        }

        // 서버 전용: XPOrbManager.TryPickup에서 호출
        public void AddXP(int amount)
        {
            if (!IsServer) return;
            if (amount <= 0)
            {
                Debug.LogWarning($"[{nameof(SharedLevelSystem)}] AddXP 유효하지 않은 값: {amount}");
                return;
            }
            SharedXP.Value += amount;
            CheckLevelUp();
        }

        // LevelUpManager.FinalizeLevelUp에서도 호출 — Playing 복귀 후 누적 XP 재검사
        internal void CheckLevelUp()
        {
            if (StageRuntime.Instance == null || StageRuntime.Instance.CurrentState.Value != GameState.Playing) return;

            int xpNeeded = XPRequired(SharedLevel.Value);
            if (SharedXP.Value < xpNeeded) return;

            // Fix: 카탈로그 검증을 XP 차감 전에 수행 — 설정 실수 시 레벨만 오르고 UI 없이 보상 소실 방지
            if (levelUpManager == null || !levelUpManager.HasValidCatalog())
            {
                Debug.LogError($"[{nameof(SharedLevelSystem)}] UpgradeCatalog 없음 또는 옵션 없음 — 레벨업 차단. XP/Level 변경 없음.");
                return;
            }

            SharedXP.Value -= xpNeeded;
            SharedLevel.Value++;
            levelUpManager.BeginLevelUp(SharedLevel.Value);
        }

        // XPRequired(level): level에서 level+1로 가는 데 필요한 XP
        public static int XPRequired(int level) =>
            Mathf.RoundToInt(10f * Mathf.Pow(level, 1.5f));

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestDebugLevelUpRpc()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GrantDebugLevelUpXP();
#endif
        }

        private void GrantDebugLevelUpXP()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsServer) return;

            if (StageRuntime.Instance == null || StageRuntime.Instance.CurrentState.Value != GameState.Playing)
            {
                Debug.LogWarning($"[{nameof(SharedLevelSystem)}] 디버그 레벨업은 Playing 상태에서만 가능합니다.");
                return;
            }

            if (levelUpManager == null || !levelUpManager.HasValidCatalog())
            {
                Debug.LogError($"[{nameof(SharedLevelSystem)}] UpgradeCatalog 없음 또는 옵션 없음 — 디버그 레벨업 불가.");
                return;
            }

            int xpNeeded = XPRequired(SharedLevel.Value);
            int grantXP  = Mathf.Max(1, Mathf.CeilToInt(xpNeeded - SharedXP.Value));
            Debug.Log($"[{nameof(SharedLevelSystem)}] 디버그 레벨업 키 입력 — XP +{grantXP}");
            AddXP(grantXP);
#endif
        }

        private void KillVisibleEnemiesByDebugKey()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (StageRuntime.Instance == null || StageRuntime.Instance.CurrentState.Value != GameState.Playing)
            {
                Debug.LogWarning($"[{nameof(SharedLevelSystem)}] 디버그 화면 처치는 Playing 상태에서만 가능합니다.");
                return;
            }

            CollectVisibleEnemyIds(visibleEnemyIds);
            if (visibleEnemyIds.Count == 0)
            {
                Debug.Log($"[{nameof(SharedLevelSystem)}] 화면 안에 처치할 적이 없습니다.");
                return;
            }

            ulong[] ids = visibleEnemyIds.ToArray();
            if (IsServer)
                KillEnemiesByIds(ids);
            else
                RequestKillVisibleEnemiesRpc(ids);
#endif
        }

        private static void CollectVisibleEnemyIds(List<ulong> results)
        {
            results.Clear();

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning($"[{nameof(SharedLevelSystem)}] MainCamera 없음 — 화면 적 처치 불가.");
                return;
            }

            EnemyNetworkBase[] enemies = FindObjectsByType<EnemyNetworkBase>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyNetworkBase enemy = enemies[i];
                if (enemy == null || !enemy.IsSpawned || !enemy.IsAlive) continue;

                Vector3 viewport = cam.WorldToViewportPoint(enemy.transform.position);
                if (viewport.z <= 0f) continue;
                if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;

                results.Add(enemy.NetworkObjectId);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestKillVisibleEnemiesRpc(ulong[] enemyNetworkObjectIds)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            KillEnemiesByIds(enemyNetworkObjectIds);
#endif
        }

        private void KillEnemiesByIds(ulong[] enemyNetworkObjectIds)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsServer || enemyNetworkObjectIds == null) return;

            int killed = 0;
            for (int i = 0; i < enemyNetworkObjectIds.Length; i++)
            {
                ulong id = enemyNetworkObjectIds[i];
                if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj)) continue;
                if (!obj.TryGetComponent<EnemyNetworkBase>(out var enemy)) continue;
                if (!enemy.IsAlive) continue;

                enemy.TakeDamage(9999999f);
                killed++;
            }

            Debug.Log($"[{nameof(SharedLevelSystem)}] 디버그 화면 적 처치 — {killed}마리");
#endif
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestSpawnDebugItemsRpc()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SpawnDebugItems();
#endif
        }

        private void SpawnDebugItems()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsServer) return;
            if (debugSpawnItems == null || debugSpawnItems.Length == 0)
            {
                Debug.LogWarning($"[{nameof(SharedLevelSystem)}] debugSpawnItems 비어 있음 — Inspector에서 아이템 할당 필요");
                return;
            }

            Vector3 spawnPos = GetDebugSpawnPosition();
            foreach (var item in debugSpawnItems)
            {
                if (item == null) continue;
                bool ok = NetworkedItemPickup.SpawnAt(item, spawnPos);
                Debug.Log($"[{nameof(SharedLevelSystem)}] 디버그 아이템 스폰 {(ok ? "성공" : "실패")}: {item.name} @ {spawnPos}");
                spawnPos += Vector3.right * 1.5f; // 겹치지 않도록 간격
            }
#endif
        }

        private Vector3 GetDebugSpawnPosition()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var client in NetworkManager.ConnectedClients.Values)
                if (client.PlayerObject != null)
                    return client.PlayerObject.transform.position + Vector3.forward * 2f;
#endif
            return Vector3.zero;
        }
    }
}
