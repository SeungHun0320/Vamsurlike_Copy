using Unity.Netcode;
using UnityEngine;
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
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        // ─── Stage Loading ──────────────────────────────────────────────
        public void LoadStage(int stageId)
        {
            if (!DataManager.Stages.TryGetValue(stageId, out activeStage))
            {
                Debug.LogError($"[{nameof(StageRuntime)}] stageId={stageId}를 DataManager에서 찾을 수 없습니다.");
                stageLoaded = false;
                return;
            }
            stageLoaded        = true;
            bossPhaseTriggered = false;
            ElapsedTime.Value  = 0f;
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
        public void CheckGameOver()
        {
            if (!IsServer) return;
            if (GameFlowCoordinator.Instance == null) return;
            if (GameFlowCoordinator.Instance.CurrentFlow.Value == GameFlowState.GameOver) return;

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) continue;
                if (client.PlayerObject == null) continue;

                var stats = client.PlayerObject.GetComponent<PlayerNetworkStats>();
                if (stats == null) continue;

                if (stats.IsAlive || stats.IsDowned.Value) return;
            }

            GameFlowCoordinator.Instance.ForceTransition(GameFlowState.GameOver);
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
