using System;

namespace Vamsurlike.UI.Events
{
    public sealed class SkillUIEventChannel
    {
        public event Action<SkillSlotsPayload>       SkillSlotsChanged;
        public event Action<UltimateCooldownPayload> UltimateCooldownChanged;

        public void PublishSkillSlotsChanged(SkillSlotsPayload p)          => SkillSlotsChanged?.Invoke(p);
        public void PublishUltimateCooldownChanged(UltimateCooldownPayload p) => UltimateCooldownChanged?.Invoke(p);
    }
}
