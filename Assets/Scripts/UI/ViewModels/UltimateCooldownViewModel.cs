using System;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.ViewModels
{
    public sealed class UltimateCooldownViewModel : ViewModelBase
    {
        public event Action<UltimateCooldownPayload> OnCooldownChanged;

        public UltimateCooldownPayload Last { get; private set; }

        protected override void Subscribe()
        {
            var hub = UIEventHub.Instance;
            if (hub == null) return;

            hub.Skill.UltimateCooldownChanged += Handle;
            if (hub.Skill.TryGetLatestUltimateCooldown(out UltimateCooldownPayload p))
                Handle(p);
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Skill.UltimateCooldownChanged -= Handle;
        }

        private void Handle(UltimateCooldownPayload p)
        {
            Last = p;
            OnCooldownChanged?.Invoke(p);
        }
    }
}
