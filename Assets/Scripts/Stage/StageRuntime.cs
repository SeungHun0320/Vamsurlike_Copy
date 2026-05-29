using Unity.Netcode;
using UnityEngine;
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
        }

        public override void OnNetworkSpawn()
        {
            CurrentState.OnValueChanged += OnGameStateChanged;

            // 늦게 참가한 클라이언트가 이미 LevelingUp 상태일 때 timeScale을 즉시 적용
            OnGameStateChanged(CurrentState.Value, CurrentState.Value);

            if (!IsServer) return;

            if (PoolManager.Instance != null) PoolManager.Instance.WarmupDeferredPools();

            waveController?.Initialize(Spawn);
            waveController?.Begin();
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

        // 서버 전용: LevelUpManager 등 다른 시스템에서 상태 전환 요청
        public void SetGameState(GameState newState)
        {
            if (!IsServer) return;
            CurrentState.Value = newState;
        }

        private void OnGameStateChanged(GameState prev, GameState next)
        {
            // LevelingUp 진입 시 전체 일시정지, 복귀 시 재개
            // UI 애니메이션은 Time.unscaledDeltaTime 사용
            Time.timeScale = next == GameState.LevelingUp ? 0f : 1f;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }
    }
}
