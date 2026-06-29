using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.UI.Events;

namespace Vamsurlike.Stage
{
    // Stage 씬의 NetworkObject에 배치. 전투 흐름과 스테이지 페이즈를 독립적으로 관리한다.
    // StageRuntime은 씬 구성(Composition Root)만 담당하고, 상태 전이 결정은 이 클래스가 단독 책임.
    public class GameFlowCoordinator : NetworkBehaviour
    {
        public static GameFlowCoordinator Instance { get; private set; }

        // 모든 클라이언트가 읽고, 서버만 쓴다.
        public NetworkVariable<GameFlowState> CurrentFlow { get; } = new(
            GameFlowState.Gameplay,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<StagePhase> CurrentPhase { get; } = new(
            StagePhase.Waves,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsGameplayActive => CurrentFlow.Value == GameFlowState.Gameplay;
        public bool IsBossPhase => CurrentPhase.Value == StagePhase.Boss;

        private readonly Queue<StateTransitionRequest> transitionQueue = new();

        private readonly struct StateTransitionRequest
        {
            public readonly GameFlowState TargetState;
            public readonly Action    OnGranted;

            public StateTransitionRequest(GameFlowState targetState, Action onGranted)
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
            CurrentFlow.OnValueChanged += OnFlowStateChanged;
            // 늦게 참가한 클라이언트가 이미 일시정지 상태일 때 timeScale 즉시 동기화
            OnFlowStateChanged(CurrentFlow.Value, CurrentFlow.Value);
        }

        public override void OnNetworkDespawn()
        {
            CurrentFlow.OnValueChanged -= OnFlowStateChanged;
            Time.timeScale = 1f;
            base.OnNetworkDespawn();
        }

        // Gameplay이면 즉시 진입 + onGranted 호출, 아니면 큐에 적재.
        // LevelingUp·ChestOpening 요청에 사용. 서버 전용.
        public void RequestTransition(GameFlowState targetState, Action onGranted)
        {
            if (!IsServer) return;

            if (IsGameplayActive)
            {
                CurrentFlow.Value = targetState;
                onGranted?.Invoke();
            }
            else
            {
                transitionQueue.Enqueue(new StateTransitionRequest(targetState, onGranted));
                Debug.Log(
                    $"[{nameof(GameFlowCoordinator)}] 전환 큐 적재: {targetState} " +
                    $"(현재={CurrentFlow.Value}, 대기={transitionQueue.Count})");
            }
        }

        // LevelUp·Chest 완료 시 호출 — 큐에 대기 전환이 있으면 소비, 없으면 Gameplay 복귀.
        public void ReturnToGameplay()
        {
            if (!IsServer) return;

            if (transitionQueue.Count > 0)
            {
                StateTransitionRequest next = transitionQueue.Dequeue();
                Debug.Log($"[{nameof(GameFlowCoordinator)}] 큐 소비: {next.TargetState} (남은 대기={transitionQueue.Count})");
                CurrentFlow.Value = next.TargetState;
                next.OnGranted?.Invoke();
            }
            else
            {
                CurrentFlow.Value = GameFlowState.Gameplay;
            }
        }

        // Clear·GameOver 등 흐름 강제 전환 — 큐를 비우고 진입한다.
        public void ForceTransition(GameFlowState next)
        {
            if (!IsServer) return;
            if (transitionQueue.Count > 0)
            {
                Debug.Log($"[{nameof(GameFlowCoordinator)}] 강제 전환으로 큐 {transitionQueue.Count}건 폐기: {next}");
                transitionQueue.Clear();
            }
            CurrentFlow.Value = next;
        }

        // Waves/Boss 전환은 전투 흐름을 건드리지 않는다. 서버 전용.
        public void SetStagePhase(StagePhase next)
        {
            if (!IsServer) return;
            CurrentPhase.Value = next;
        }

        private void OnFlowStateChanged(GameFlowState prev, GameFlowState next)
        {
            // LevelingUp·ChestOpening·Clear: UI 동안 일시정지
            // GameOver·Gameplay: 시간 흐름 유지
            // UI 애니메이션은 Time.unscaledDeltaTime 사용할 것
            bool shouldPause = next is GameFlowState.LevelingUp
                                    or GameFlowState.ChestOpening
                                    or GameFlowState.Clear;
            Time.timeScale = shouldPause ? 0f : 1f;

            UIEventHub.Instance?.Flow.PublishGameFlowChanged(new GameFlowPayload(prev, next));
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }
    }
}
