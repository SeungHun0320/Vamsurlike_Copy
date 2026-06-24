using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Core;
using Vamsurlike.Network;

namespace Vamsurlike.Stage
{
    // Stage 씬에 배치. 서버 전용 시스템 Composition Root.
    public class StageRuntime : NetworkBehaviour
    {
        public static StageRuntime Instance { get; private set; }

        [SerializeField] private WaveController waveController;
        [SerializeField] private DropManager    dropManager;

        public WaveController    Wave  => waveController;
        public DropManager       Drops => dropManager;
        public EnemySpawnManager Spawn => EnemySpawnManager.Instance;

        // 모든 클라이언트가 읽고, 서버만 쓴다.
        public NetworkVariable<GameState> CurrentState { get; } = new(
            GameState.Playing,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public float ElapsedTime { get; private set; }

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;

            StartupValidator.ValidateStage(waveController, dropManager);
        }

        public override void OnNetworkSpawn()
        {
            CurrentState.OnValueChanged += OnGameStateChanged;

            // 늦게 참가한 클라이언트가 이미 LevelingUp 상태일 때 timeScale을 즉시 적용
            OnGameStateChanged(CurrentState.Value, CurrentState.Value);

            if (!IsServer) return;

            if (PoolManager.Instance != null) PoolManager.Instance.WarmupDeferredPools();

            if (waveController == null || dropManager == null) return;

            waveController.Initialize(Spawn);
            waveController.Begin();
        }

        public override void OnNetworkDespawn()
        {
            CurrentState.OnValueChanged -= OnGameStateChanged;
            Time.timeScale = 1f;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer) return;
            if (CurrentState.Value != GameState.Playing) return;
            ElapsedTime += Time.deltaTime;
        }

        // 현재 상태가 expected일 때만 next로 전환. 실패 시 false 반환.
        // LevelingUp·ChestOpening 등 Playing에서만 시작해야 하는 전환에 사용.
        public bool TryTransition(GameState expected, GameState next)
        {
            if (!IsServer) return false;
            if (CurrentState.Value != expected)
            {
                Debug.LogWarning(
                    $"[{nameof(StageRuntime)}] 상태 전환 실패: 현재={CurrentState.Value}, " +
                    $"기대={expected}, 목표={next}");
                return false;
            }
            CurrentState.Value = next;
            return true;
        }

        // Clear·GameOver·BossPhase처럼 현재 상태와 무관하게 강제 전환해야 하는 경우에만 사용.
        public void ForceTransition(GameState next)
        {
            if (!IsServer) return;
            CurrentState.Value = next;
        }

        private void OnGameStateChanged(GameState prev, GameState next)
        {
            // LevelingUp·ChestOpening 진입 시 전체 일시정지, 복귀 시 재개
            // UI 애니메이션은 Time.unscaledDeltaTime 사용
            bool shouldPause = next == GameState.LevelingUp || next == GameState.ChestOpening;
            Time.timeScale = shouldPause ? 0f : 1f;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }
    }
}
