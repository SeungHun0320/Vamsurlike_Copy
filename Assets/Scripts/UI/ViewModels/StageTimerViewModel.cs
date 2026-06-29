using System;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.ViewModels
{
    public sealed class StageTimerViewModel : ViewModelBase
    {
        public event Action<StageTimerPayload> OnTimerUpdated;

        public StageTimerPayload Last { get; private set; }

        protected override void Subscribe()
        {
            var hub = UIEventHub.Instance;
            if (hub == null) return;

            hub.Stage.StageTimerChanged += Handle;
            if (hub.Stage.TryGetLatestStageTimer(out StageTimerPayload payload))
                Handle(payload);
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Stage.StageTimerChanged -= Handle;
        }

        private void Handle(StageTimerPayload payload)
        {
            Last = payload;
            OnTimerUpdated?.Invoke(payload);
        }
    }
}
