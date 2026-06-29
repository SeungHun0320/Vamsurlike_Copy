using System;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.ViewModels
{
    public class SkillHUDViewModel : ViewModelBase
    {
        public event Action<SkillSlotsPayload> OnSkillsChanged;

        // 초기 렌더 시 View가 직접 읽는 현재 상태
        public SkillSlotsPayload Current { get; private set; }

        protected override void Subscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Skill.SkillSlotsChanged += Handle;
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Skill.SkillSlotsChanged -= Handle;
        }

        private void Handle(SkillSlotsPayload payload)
        {
            Current = payload;
            OnSkillsChanged?.Invoke(payload);
        }
    }
}
