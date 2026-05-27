using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Stage
{
    // Stage 씬에 배치. 서버 전용 시스템 Composition Root.
    // Phase 7: NetworkVariable<GameState>, 보스 페이즈, 클리어/패배 연결
    public class StageRuntime : NetworkBehaviour
    {
        public static StageRuntime Instance { get; private set; }

        [SerializeField] private WaveController waveController;
        [SerializeField] private DropManager    dropManager;

        public WaveController Wave  => waveController;
        public DropManager    Drops => dropManager;

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) { enabled = false; return; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
