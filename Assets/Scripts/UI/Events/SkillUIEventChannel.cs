using System;

namespace Vamsurlike.UI.Events
{
    public sealed class SkillUIEventChannel
    {
        private SkillSlotsPayload latestSkillSlots;
        private UltimateCooldownPayload latestUltimateCooldown;
        private bool hasSkillSlots;
        private bool hasUltimateCooldown;

        public event Action<SkillSlotsPayload>       SkillSlotsChanged;
        public event Action<UltimateCooldownPayload> UltimateCooldownChanged;

        public bool TryGetLatestSkillSlots(out SkillSlotsPayload payload)
        {
            payload = latestSkillSlots;
            return hasSkillSlots;
        }

        public bool TryGetLatestUltimateCooldown(out UltimateCooldownPayload payload)
        {
            payload = latestUltimateCooldown;
            return hasUltimateCooldown;
        }

        public void PublishSkillSlotsChanged(SkillSlotsPayload p)
        {
            latestSkillSlots = p;
            hasSkillSlots = true;
            SkillSlotsChanged?.Invoke(p);
        }

        public void PublishUltimateCooldownChanged(UltimateCooldownPayload p)
        {
            latestUltimateCooldown = p;
            hasUltimateCooldown = true;
            UltimateCooldownChanged?.Invoke(p);
        }
    }
}
