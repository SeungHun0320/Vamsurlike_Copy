using UnityEngine;
using Vamsurlike.Stage;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.Adapters
{
    // Stage 씬 Canvas/Manager 오브젝트에 배치.
    // StageRuntime.ElapsedTime + GameFlowCoordinator.CurrentPhase →
    // UIEventHub.Stage.StageTimerChanged 발행.
    public sealed class StageTimerAdapter : MonoBehaviour
    {
        private bool subscribed;

        private void Update()
        {
            if (subscribed) return;
            if (StageRuntime.Instance == null || GameFlowCoordinator.Instance == null) return;
            if (UIEventHub.Instance == null) return;

            StageRuntime.Instance.ElapsedTime.OnValueChanged         += OnElapsedChanged;
            GameFlowCoordinator.Instance.CurrentPhase.OnValueChanged += OnPhaseChanged;
            subscribed = true;

            Publish(); // 현재 상태 즉시 발행
        }

        private void OnDestroy()
        {
            if (!subscribed) return;
            if (StageRuntime.Instance != null)
                StageRuntime.Instance.ElapsedTime.OnValueChanged -= OnElapsedChanged;
            if (GameFlowCoordinator.Instance != null)
                GameFlowCoordinator.Instance.CurrentPhase.OnValueChanged -= OnPhaseChanged;
        }

        private void OnElapsedChanged(float _, float __)  => Publish();
        private void OnPhaseChanged(StagePhase _, StagePhase __) => Publish();

        private void Publish()
        {
            if (UIEventHub.Instance == null) return;

            float elapsed      = StageRuntime.Instance != null ? StageRuntime.Instance.ElapsedTime.Value : 0f;
            float bossTime     = StageRuntime.Instance != null ? StageRuntime.Instance.StageDuration     : 0f;
            bool  isBossPhase  = GameFlowCoordinator.Instance != null && GameFlowCoordinator.Instance.IsBossPhase;

            UIEventHub.Instance.Stage.PublishStageTimer(
                new StageTimerPayload(elapsed, bossTime, isBossPhase));
        }
    }
}
