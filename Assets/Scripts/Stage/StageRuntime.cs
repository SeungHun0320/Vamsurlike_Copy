using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Core;
using Vamsurlike.Network;

namespace Vamsurlike.Stage
{
    // Stage 씬에 배치. 서버 전용 시스템 Composition Root.
    // 게임 상태(CurrentState) 관리는 GameFlowCoordinator가 전담한다.
    public class StageRuntime : NetworkBehaviour
    {
        public static StageRuntime Instance { get; private set; }

        [SerializeField] private WaveController waveController;
        [SerializeField] private DropManager    dropManager;

        public WaveController    Wave  => waveController;
        public DropManager       Drops => dropManager;
        public EnemySpawnManager Spawn => EnemySpawnManager.Instance;

        public float ElapsedTime { get; private set; }

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

            waveController.Initialize(Spawn);
            waveController.Begin();
        }

        private void Update()
        {
            if (!IsServer) return;
            if (GameFlowCoordinator.Instance == null) return;
            if (GameFlowCoordinator.Instance.CurrentState.Value != GameState.Playing) return;
            ElapsedTime += Time.deltaTime;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }
    }
}
