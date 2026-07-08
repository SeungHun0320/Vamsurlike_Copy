using System;
using System.Collections.Generic;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.ViewModels
{
    public readonly struct SkillHUDSlotViewData
    {
        public readonly string Name;
        public readonly int Level;
        public readonly int MaxLevel;
        public bool IsMaxLevel => MaxLevel > 0 && Level >= MaxLevel;

        public SkillHUDSlotViewData(string name, int level, int maxLevel = 0)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "-" : name;
            Level = Math.Max(1, level);
            MaxLevel = Math.Max(0, maxLevel);
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
                int maxLevel = payload.MaxLevels != null && i < payload.MaxLevels.Length
                    ? payload.MaxLevels[i]
                    : 0;
                slots.Add(new SkillHUDSlotViewData(payload.Names[i], level, maxLevel));
            }

            OnSkillsChanged?.Invoke(slots);
        }
    }
}
