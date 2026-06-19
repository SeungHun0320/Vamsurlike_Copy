using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    internal sealed class SkillRuntimeState
    {
        public SkillDataSO Skill { get; }
        public int Level { get; private set; }
        public float CooldownTimer { get; set; }
        public bool IsActive { get; set; } = true;
        public float DurationTimer { get; set; } = -1f;
        public float TickTimer { get; set; }

        public SkillRuntimeState(SkillDataSO skill, int level)
        {
            Skill = skill;
            Level = Mathf.Max(1, level);
        }

        public bool TryLevelUp()
        {
            if (Skill == null) return false;

            int maxLevel = Mathf.Max(1, Skill.maxLevel);
            if (Level >= maxLevel) return false;

            Level++;
            return true;
        }
    }
}
