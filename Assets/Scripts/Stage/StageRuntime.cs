using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vamsurlike.Core;
using Vamsurlike.Data.Runtime;
using Vamsurlike.Network;
using Vamsurlike.Player;

namespace Vamsurlike.Stage
{
    // Stage 씬에 배치. 서버 전용 시스템 Composition Root.
    // 게임 흐름(CurrentFlow)과 스테이지 페이즈(CurrentPhase)는 GameFlowCoordinator가 전담한다.
    public class StageRuntime : NetworkBehaviour
    {
        public static StageRuntime Instance { get; private set; }

        [SerializeField] private WaveController waveController;
        [SerializeField] private DropManager    dropManager;

        public WaveController    Wave  => waveController;
        public DropManager       Drops => dropManager;
        public EnemySpawnManager Spawn => EnemySpawnManager.Instance;

        // 전체 클라이언트에 동기화 — HUD 타이머 표시용
        public NetworkVariable<float> ElapsedTime { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> StageDurationSeconds { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 어댑터에서 타이머 페이로드 계산 시 사용 (0이면 스테이지 미로드)
        public float StageDuration => StageDurationSeconds.Value;

        // 모든 클라이언트의 씬 동기화가 끝나 웨이브/드랍 스폰이 시작된 뒤에만 true — 스킬 캐스팅
        // (투사체 스폰) 쪽에서도 같은 시점까지 대기하도록 SkillManager.Update()가 이 값을 확인한다.
        public bool IsWaveSystemReady { get; private set; }

        private StageData activeStage;
        private bool      stageLoaded;
        private bool      bossPhaseTriggered;

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;

            StartupValidator.ValidateStage(waveController, dropManager);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            DataManager.Initialize();

            if (PoolManager.Instance != null) PoolManager.Instance.WarmupDeferredPools();

            if (waveController == null || dropManager == null) return;

            LoadStage(1);
            if (!stageLoaded) return;

            waveController.Initialize(Spawn, activeStage.WaveGroupId);

            // 서버 자신의 씬 로드가 끝나자마자 바로 웨이브를 시작하면, 아직 씬 동기화 중인(느리게
            // 접속한) 클라이언트에게는 스폰 메시지가 도착하기도 전에 적/투사체의 NetworkTransform
            // 갱신 메시지가 먼저 도착해버려 "스폰 안 된 오브젝트" 취급으로 버려진다(10초 뒤 타임아웃
            // 경고, 그 오브젝트는 그 클라에서 영원히 안 보임). 플레이어 스폰(NetworkPlayerSpawner)이
            // 이미 쓰고 있는 것과 동일한 패턴 — 모든 클라이언트의 씬 동기화가 끝나는 시점까지
            // 대기했다가 웨이브를 시작한다.
            if (NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
            else
                StartWaveSystem();
        }

        private void HandleLoadEventCompleted(
            string sceneName,
            LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            if (sceneName != gameObject.scene.name) return;

            if (NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;

            StartWaveSystem();
        }

        private void StartWaveSystem()
        {
            IsWaveSystemReady = true;
            waveController.Begin();
        }

        private void Update()
        {
            if (!IsServer) return;
            if (GameFlowCoordinator.Instance == null) return;
            if (!GameFlowCoordinator.Instance.IsGameplayActive) return;

            ElapsedTime.Value += Time.deltaTime;
            CheckBossPhase();
        }

        public override void OnDestroy()
        {
            if (NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;

            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        // ─── Stage Loading ──────────────────────────────────────────────
        public void LoadStage(int stageId)
        {
            if (!IsServer) return;

            if (!DataManager.Stages.TryGetValue(stageId, out activeStage))
            {
                Debug.LogError($"[{nameof(StageRuntime)}] stageId={stageId}를 DataManager에서 찾을 수 없습니다.");
                stageLoaded = false;
                StageDurationSeconds.Value = 0f;
                return;
            }
            stageLoaded        = true;
            bossPhaseTriggered = false;
            ElapsedTime.Value  = 0f;
            StageDurationSeconds.Value = activeStage.DurationSeconds;
            GameFlowCoordinator.Instance?.SetStagePhase(StagePhase.Waves);
        }

        // ─── Boss Phase Trigger ─────────────────────────────────────────
        private void CheckBossPhase()
        {
            if (!stageLoaded || bossPhaseTriggered) return;
            if (ElapsedTime.Value < activeStage.DurationSeconds) return;

            bossPhaseTriggered = true;

            if (!activeStage.HasBoss)
            {
                if (activeStage.ClearCondition == StageClearCondition.TimeSurvival)
                    if (GameFlowCoordinator.Instance != null)
                        GameFlowCoordinator.Instance.ForceTransition(GameFlowState.Clear);
                return;
            }

            if (GameFlowCoordinator.Instance != null)
                GameFlowCoordinator.Instance.SetStagePhase(StagePhase.Boss);
            if (EnemySpawnManager.Instance != null)
                EnemySpawnManager.Instance.SpawnBossByName(activeStage.BossEnemyName);
        }

        // ─── Game Over Check (서버 전용) ────────────────────────────────
        // CanAct=true인 플레이어가 한 명이라도 있으면 GameOver 아님.
        // Downed(1단계)·DeadWaiting(2단계) 모두 CanAct=false이므로 하나의 조건으로 커버.
        public void CheckGameOver()
        {
            if (!IsServer) return;
            if (GameFlowCoordinator.Instance == null) return;
            if (GameFlowCoordinator.Instance.CurrentFlow.Value == GameFlowState.GameOver) return;

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) continue;
                if (client.PlayerObject == null) continue;

                if (!client.PlayerObject.TryGetComponent(out PlayerNetworkStats stats)) continue;

                if (stats.CanAct) return;
            }

            GameFlowCoordinator.Instance.ForceTransition(GameFlowState.GameOver);

            // GameOver 확정 — 모든 플레이어의 부활 타이머 즉시 중단
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) continue;
                if (client.PlayerObject == null) continue;
                if (client.PlayerObject.TryGetComponent(out PlayerReviveHandler reviveHandler))
                    reviveHandler.StopAllReviveActivity();
            }
        }

        // ─── Debug ─────────────────────────────────────────────────────
        public void DebugSkipTime(float seconds)
        {
            if (!IsServer) return;
            ElapsedTime.Value += seconds;
        }

        public void DebugTriggerBossPhase()
        {
            if (!IsServer) return;
            if (!stageLoaded)
            {
                Debug.LogWarning($"[{nameof(StageRuntime)}] 스테이지 미로드 — 보스 페이즈 스킵 불가");
                return;
            }
            if (bossPhaseTriggered)
            {
                Debug.LogWarning($"[{nameof(StageRuntime)}] 이미 보스 페이즈 진입됨");
                return;
            }
            if (!activeStage.HasBoss)
            {
                Debug.LogWarning($"[{nameof(StageRuntime)}] StageTable.bossEnemyName이 비어 있습니다.");
                return;
            }

            bossPhaseTriggered = true;
            if (GameFlowCoordinator.Instance != null)
                GameFlowCoordinator.Instance.SetStagePhase(StagePhase.Boss);
            if (EnemySpawnManager.Instance != null)
                EnemySpawnManager.Instance.SpawnBossByName(activeStage.BossEnemyName);
            Debug.Log($"[{nameof(StageRuntime)}] 디버그: 보스 페이즈 강제 진입");
        }
    }
}
