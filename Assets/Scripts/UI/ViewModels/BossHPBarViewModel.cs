using System;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.ViewModels
{
    public sealed class BossHPBarViewModel : ViewModelBase
    {
        public event Action<BossStatusPayload> OnBossVisible;
        public event Action                    OnBossHidden;

        protected override void Subscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Stage.BossStatusChanged += Handle;
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Stage.BossStatusChanged -= Handle;
        }

        private void Handle(BossStatusPayload payload)
        {
            if (payload.IsVisible) OnBossVisible?.Invoke(payload);
            else                   OnBossHidden?.Invoke();
        }
    }
}
