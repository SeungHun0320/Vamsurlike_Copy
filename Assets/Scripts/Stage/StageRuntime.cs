using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Core;
using Vamsurlike.Network;
using Vamsurlike.Player;

namespace Vamsurlike.Stage
{
    // Stage 씬에 배치. 서버 전용 시스템 Composition Root.
    // 게임 흐름(CurrentFlow)과 스테이지 페이즈(CurrentPhase)는 GameFlowCoordinator가 전담한다.
    public class StageRuntime : NetworkBehaviour
    {
        public static StageRuntime Instance { get; private set; }

        [SerializeField] private WaveController  waveController;
        [SerializeField] private DropManager     dropManager;
        [SerializeField] private StageTableSO    stageTable;

        public WaveController    Wave  => waveController;
        public DropManager       Drops => dropManager;
        public EnemySpawnManager Spawn => EnemySpawnManager.Instance;

        // 전체 클라이언트에 동기화 — HUD 타이머 표시용
        public NetworkVariable<float> ElapsedTime { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private StageRow activeStage;
        private bool     stageLoaded;
        private bool     bossPhaseTriggered;

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;

            StartupValidator.ValidateStage(waveController, dropManager);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            if (PoolManager.Instance != null) PoolManager.Instance.WarmupDeferredPools();

            if (waveController == null || dropManager == null) return;

            LoadStage(1);
            if (!stageLoaded) return;

            waveController.Initialize(Spawn, activeStage.waveGroupId);
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
            if (stageTable == null || !stageTable.TryGetStage(stageId, out activeStage))
            {
                Debug.LogError($"[{nameof(StageRuntime)}] stageId={stageId}를 테이블에서 찾을 수 없습니다.");
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
            if (ElapsedTime.Value < activeStage.durationSeconds) return;

            bossPhaseTriggered = true;

            if (activeStage.bossData == null)
            {
                // 보스 없음 + TimeSurvival → 즉시 클리어
                if (activeStage.clearCondition == StageClearCondition.TimeSurvival)
                    GameFlowCoordinator.Instance?.ForceTransition(GameFlowState.Clear);
                return;
            }

            GameFlowCoordinator.Instance?.SetStagePhase(StagePhase.Boss);
            EnemySpawnManager.Instance?.SpawnBoss(activeStage.bossData);
        }

        // ─── Game Over Check (서버 전용) ────────────────────────────────
        // PlayerReviveHandler 다운 타이머 만료 후 호출.
        // 다운 중(IsDowned=true)인 플레이어는 아직 구출 가능하므로 GameOver 아님.
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

                // 살아있거나 아직 다운 타이머가 남아있으면 Game Over 아님
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
            if (activeStage.bossData == null)
            {
                Debug.LogWarning($"[{nameof(StageRuntime)}] StageTable.bossData가 null — Setup Stage Assets 메뉴를 실행하세요");
                return;
            }

            bossPhaseTriggered = true;

            if (GameFlowCoordinator.Instance != null)
                GameFlowCoordinator.Instance.SetStagePhase(StagePhase.Boss);
            if (EnemySpawnManager.Instance != null)
                EnemySpawnManager.Instance.SpawnBoss(activeStage.bossData);
            Debug.Log($"[{nameof(StageRuntime)}] 디버그: 보스 페이즈 강제 진입");
        }
    }
}
