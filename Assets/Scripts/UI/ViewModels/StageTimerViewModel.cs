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
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Stage.StageTimerChanged += Handle;
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
