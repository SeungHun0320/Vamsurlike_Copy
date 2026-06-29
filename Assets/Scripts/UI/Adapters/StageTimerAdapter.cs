using UnityEngine;
using Vamsurlike.Stage;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.Adapters
{
    // Stage 씬에 배치.
    // StageRuntime.ElapsedTime + GameFlowCoordinator.CurrentPhase →
    // UIEventHub.Stage.StageTimerChanged 발행.
    // OnValueChanged는 Host 자기 쓰기 시 미보장이므로 Update()에서 매 프레임 발행한다.
    public sealed class StageTimerAdapter : MonoBehaviour
    {
        private bool ready;
        private int  lastPublishedSecond = -1;

        private void Update()
        {
            if (!ready)
            {
                if (StageRuntime.Instance == null || GameFlowCoordinator.Instance == null) return;
                if (UIEventHub.Instance == null) return;
                ready = true;
            }

            // 표시 단위(초)가 바뀔 때만 Publish — 매 프레임 string alloc 방지
            int currentSecond = Mathf.FloorToInt(
                StageRuntime.Instance != null ? StageRuntime.Instance.ElapsedTime.Value : 0f);
            if (currentSecond == lastPublishedSecond) return;
            lastPublishedSecond = currentSecond;

            Publish();
        }

        private void Publish()
        {
            if (UIEventHub.Instance == null) return;

            float elapsed     = StageRuntime.Instance != null ? StageRuntime.Instance.ElapsedTime.Value : 0f;
            float bossTime    = StageRuntime.Instance != null ? StageRuntime.Instance.StageDuration     : 0f;
            bool  isBossPhase = GameFlowCoordinator.Instance != null && GameFlowCoordinator.Instance.IsBossPhase;

            UIEventHub.Instance.Stage.PublishStageTimer(
                new StageTimerPayload(elapsed, bossTime, isBossPhase));
        }
    }
}
