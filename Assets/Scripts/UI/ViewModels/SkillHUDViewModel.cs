using System;
using System.Collections.Generic;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.ViewModels
{
    public readonly struct SkillHUDSlotViewData
    {
        public readonly string Name;
        public readonly int Level;

        public SkillHUDSlotViewData(string name, int level)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "-" : name;
            Level = Math.Max(1, level);
        }
    }

    public sealed class SkillHUDViewModel : ViewModelBase
    {
        private readonly List<SkillHUDSlotViewData> slots = new();

        public event Action<IReadOnlyList<SkillHUDSlotViewData>> OnSkillsChanged;

        public IReadOnlyList<SkillHUDSlotViewData> Current => slots;

        protected override void Subscribe()
        {
            var hub = UIEventHub.Instance;
            if (hub == null) return;

            hub.Skill.SkillSlotsChanged += Handle;
            if (hub.Skill.TryGetLatestSkillSlots(out SkillSlotsPayload payload))
                Handle(payload);
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Skill.SkillSlotsChanged -= Handle;
        }

        private void Handle(SkillSlotsPayload payload)
        {
            slots.Clear();

            int count = payload.Names?.Length ?? 0;
            for (int i = 0; i < count; i++)
            {
                int level = payload.Levels != null && i < payload.Levels.Length
                    ? payload.Levels[i]
                    : 1;
                slots.Add(new SkillHUDSlotViewData(payload.Names[i], level));
            }

            OnSkillsChanged?.Invoke(slots);
        }
    }
}
