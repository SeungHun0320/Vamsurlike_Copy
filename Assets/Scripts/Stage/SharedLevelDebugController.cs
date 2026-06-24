using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Vamsurlike.Data;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Stage
{
    [RequireComponent(typeof(SharedLevelSystem))]
    [RequireComponent(typeof(LevelUpManager))]
    public class SharedLevelDebugController : NetworkBehaviour
    {
        [Header("Level Up")]
        [SerializeField] private bool enableDebugLevelUpKey = true;
        [SerializeField] private Key debugLevelUpKey = Key.L;

        [Header("Enemies")]
        [SerializeField] private bool enableDebugKillVisibleKey = true;
        [SerializeField] private Key debugKillVisibleEnemiesKey = Key.K;

        [Header("Items")]
        [SerializeField] private bool enableDebugSpawnItemKey = true;
        [SerializeField] private Key debugSpawnItemKey = Key.I;
        [SerializeField] private ItemDataSO[] debugSpawnItems;

        [Header("Wave")]
        [SerializeField] private bool enableDebugRespawnWaveKey = true;
        [SerializeField] private Key debugRespawnWaveKey = Key.R;

        [Header("Boss Phase")]
        [SerializeField] private bool enableDebugBossPhaseKey = true;
        [SerializeField] private Key  debugBossPhaseKey       = Key.T;

        private readonly List<ulong> visibleEnemyIds = new();
        private SharedLevelSystem sharedLevelSystem;
        private LevelUpManager levelUpManager;

        private void Awake()
        {
            sharedLevelSystem = GetComponent<SharedLevelSystem>();
            levelUpManager = GetComponent<LevelUpManager>();
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsSpawned) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (enableDebugLevelUpKey && keyboard[debugLevelUpKey].wasPressedThisFrame)
                RunOrRequestDebugLevelUp();

            if (enableDebugKillVisibleKey && keyboard[debugKillVisibleEnemiesKey].wasPressedThisFrame)
                RunOrRequestKillVisibleEnemies();

            if (enableDebugSpawnItemKey && keyboard[debugSpawnItemKey].wasPressedThisFrame)
                RunOrRequestSpawnItems();

            if (enableDebugRespawnWaveKey && keyboard[debugRespawnWaveKey].wasPressedThisFrame)
                RunOrRequestRespawnWave();

            if (enableDebugBossPhaseKey && keyboard[debugBossPhaseKey].wasPressedThisFrame)
                RunOrRequestBossPhase();
#endif
        }

        private void RunOrRequestDebugLevelUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsServer)
                GrantDebugLevelUpXP();
            else
                RequestDebugLevelUpRpc();
#endif
        }

        private void RunOrRequestKillVisibleEnemies()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsGameplayActive()) return;

            DebugEnemyCommands.CollectVisibleEnemyIds(visibleEnemyIds);
            if (visibleEnemyIds.Count == 0)
            {
                Debug.Log($"[{nameof(SharedLevelDebugController)}] No visible enemies to kill.");
                return;
            }

            ulong[] ids = visibleEnemyIds.ToArray();
            if (IsServer)
                KillEnemiesByIds(ids);
            else
                RequestKillVisibleEnemiesRpc(ids);
#endif
        }

        private void RunOrRequestSpawnItems()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsServer)
                SpawnDebugItems();
            else
                RequestSpawnDebugItemsRpc();
#endif
        }

        private void RunOrRequestRespawnWave()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsServer)
                RespawnWave();
            else
                RequestRespawnWaveRpc();
#endif
        }

        private void RunOrRequestBossPhase()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsServer)
                TriggerBossPhase();
            else
                RequestBossPhaseRpc();
#endif
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestDebugLevelUpRpc()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GrantDebugLevelUpXP();
#endif
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestKillVisibleEnemiesRpc(ulong[] enemyNetworkObjectIds)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            KillEnemiesByIds(enemyNetworkObjectIds);
#endif
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestSpawnDebugItemsRpc()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SpawnDebugItems();
#endif
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestRespawnWaveRpc()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RespawnWave();
#endif
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestBossPhaseRpc()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TriggerBossPhase();
#endif
        }

        private void GrantDebugLevelUpXP()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsServer || sharedLevelSystem == null) return;
            if (!IsGameplayActive()) return;

            if (levelUpManager == null || !levelUpManager.HasValidCatalog())
            {
                Debug.LogError($"[{nameof(SharedLevelDebugController)}] UpgradeCatalog is missing or empty. Debug level-up blocked.");
                return;
            }

            int xpNeeded = SharedLevelSystem.XPRequired(sharedLevelSystem.SharedLevel.Value);
            int grantXP = Mathf.Max(1, Mathf.CeilToInt(xpNeeded - sharedLevelSystem.SharedXP.Value));
            Debug.Log($"[{nameof(SharedLevelDebugController)}] Debug level-up XP +{grantXP}");
            sharedLevelSystem.AddXP(grantXP);
#endif
        }

        private void KillEnemiesByIds(ulong[] enemyNetworkObjectIds)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsServer) return;

            int killed = DebugEnemyCommands.KillEnemiesByIds(NetworkManager, enemyNetworkObjectIds);
            Debug.Log($"[{nameof(SharedLevelDebugController)}] Debug killed visible enemies: {killed}");
#endif
        }

        private void SpawnDebugItems()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsServer) return;

            Vector3 spawnPosition = GetDebugSpawnPosition();
            int spawned = DebugItemSpawnCommands.SpawnItems(debugSpawnItems, spawnPosition);
            Debug.Log($"[{nameof(SharedLevelDebugController)}] Debug spawned items: {spawned}");
#endif
        }

        private void RespawnWave()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsServer) return;

            DebugWaveCommands.RespawnWave(StageRuntime.Instance);
#endif
        }

        private void TriggerBossPhase()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsServer) return;

            if (StageRuntime.Instance == null)
            {
                Debug.LogWarning($"[{nameof(SharedLevelDebugController)}] StageRuntime 없음 — 보스 페이즈 스킵 불가");
                return;
            }
            StageRuntime.Instance.DebugTriggerBossPhase();
#endif
        }

        private bool IsGameplayActive()
        {
            if (GameFlowCoordinator.Instance != null && GameFlowCoordinator.Instance.IsGameplayActive)
                return true;

            Debug.LogWarning($"[{nameof(SharedLevelDebugController)}] Debug command is only available during active gameplay.");
            return false;
        }

        private Vector3 GetDebugSpawnPosition()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var client in NetworkManager.ConnectedClients.Values)
                if (client.PlayerObject != null)
                    return client.PlayerObject.transform.position + Vector3.forward * 2f;
#endif
            return transform.position;
        }
    }
}
