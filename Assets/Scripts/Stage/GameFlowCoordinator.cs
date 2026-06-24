using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Stage
{
    // Stage 씬의 NetworkObject에 배치. 게임 진행 상태(GameState) + 전환 큐 전담.
    // StageRuntime은 씬 구성(Composition Root)만 담당하고, 상태 전이 결정은 이 클래스가 단독 책임.
    public class GameFlowCoordinator : NetworkBehaviour
    {
        public static GameFlowCoordinator Instance { get; private set; }

        // 모든 클라이언트가 읽고, 서버만 쓴다.
        public NetworkVariable<GameState> CurrentState { get; } = new(
            GameState.Playing,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly Queue<StateTransitionRequest> transitionQueue = new();

        private readonly struct StateTransitionRequest
        {
            public readonly GameState TargetState;
            public readonly Action    OnGranted;

            public StateTransitionRequest(GameState targetState, Action onGranted)
            {
                TargetState = targetState;
                OnGranted   = onGranted;
            }
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            CurrentState.OnValueChanged += OnGameStateChanged;
            // 늦게 참가한 클라이언트가 이미 일시정지 상태일 때 timeScale 즉시 동기화
            OnGameStateChanged(CurrentState.Value, CurrentState.Value);
        }

        public override void OnNetworkDespawn()
        {
            CurrentState.OnValueChanged -= OnGameStateChanged;
            Time.timeScale = 1f;
            base.OnNetworkDespawn();
        }

        // Playing이면 즉시 진입 + onGranted 호출, 아니면 큐에 적재.
        // LevelingUp·ChestOpening 요청에 사용. 서버 전용.
        public void RequestTransition(GameState targetState, Action onGranted)
        {
            if (!IsServer) return;

            if (CurrentState.Value == GameState.Playing)
            {
                CurrentState.Value = targetState;
                onGranted?.Invoke();
            }
            else
            {
                transitionQueue.Enqueue(new StateTransitionRequest(targetState, onGranted));
                Debug.Log(
                    $"[{nameof(GameFlowCoordinator)}] 전환 큐 적재: {targetState} " +
                    $"(현재={CurrentState.Value}, 대기={transitionQueue.Count})");
            }
        }

        // LevelUp·Chest 완료 시 호출 — 큐에 대기 전환이 있으면 소비, 없으면 Playing 복귀.
        public void ReturnToPlaying()
        {
            if (!IsServer) return;

            if (transitionQueue.Count > 0)
            {
                StateTransitionRequest next = transitionQueue.Dequeue();
                Debug.Log($"[{nameof(GameFlowCoordinator)}] 큐 소비: {next.TargetState} (남은 대기={transitionQueue.Count})");
                CurrentState.Value = next.TargetState;
                next.OnGranted?.Invoke();
            }
            else
            {
                CurrentState.Value = GameState.Playing;
            }
        }

        // Clear·GameOver·BossPhase 등 긴급 전환 — 큐를 비우고 강제 진입.
        public void ForceTransition(GameState next)
        {
            if (!IsServer) return;
            if (transitionQueue.Count > 0)
            {
                Debug.Log($"[{nameof(GameFlowCoordinator)}] 강제 전환으로 큐 {transitionQueue.Count}건 폐기: {next}");
                transitionQueue.Clear();
            }
            CurrentState.Value = next;
        }

        private void OnGameStateChanged(GameState _, GameState next)
        {
            // LevelingUp·ChestOpening 진입 시 전체 일시정지, 복귀 시 재개
            // UI 애니메이션은 Time.unscaledDeltaTime 사용할 것
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
