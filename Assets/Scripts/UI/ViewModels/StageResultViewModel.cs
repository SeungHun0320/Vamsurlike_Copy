using System;
using Vamsurlike.Stage;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.ViewModels
{
    public class StageResultViewModel : ViewModelBase
    {
        public event Action OnShowClear;
        public event Action OnShowGameOver;
        public event Action OnHide;

        protected override void Subscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Flow.GameFlowChanged += Handle;
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Flow.GameFlowChanged -= Handle;
        }

        private void Handle(GameFlowPayload payload)
        {
            if      (payload.Next == GameFlowState.Clear)    OnShowClear?.Invoke();
            else if (payload.Next == GameFlowState.GameOver) OnShowGameOver?.Invoke();
            else                                             OnHide?.Invoke();
        }
    }
}
