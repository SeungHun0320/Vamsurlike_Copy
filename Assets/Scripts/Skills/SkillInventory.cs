using System;
using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    internal sealed class SkillInventory
    {
        private readonly List<SkillRuntimeState> skills = new();
        private readonly HashSet<SkillDataSO> evolutionLockedSkills = new();

        public IReadOnlyList<SkillRuntimeState> Skills => skills;
        public int Count => skills.Count;

        public void InitializeStartingSkills(SkillDataSO[] startingSkills, Action<string> logWarning)
        {
            skills.Clear();
            if (startingSkills == null) return;

            for (int i = 0; i < startingSkills.Length; i++)
            {
                SkillDataSO skill = startingSkills[i];
                if (skill == null)
                {
                    logWarning?.Invoke($"startingSkills[{i}] is null.");
                    continue;
                }

                if (TryGet(skill, out var owned))
                {
                    owned.TryLevelUp();
                    continue;
                }

                skills.Add(new SkillRuntimeState(skill, 1));
            }
        }

        public bool Learn(SkillDataSO skill, float retryDelay, out bool upgradedExisting)
        {
            upgradedExisting = false;
            if (skill == null || IsEvolutionLocked(skill)) return false;

            if (TryGet(skill, out var owned))
            {
                upgradedExisting = true;
                return UpgradeOwned(owned, retryDelay);
            }

            skills.Add(new SkillRuntimeState(skill, 1));
            return true;
        }

        public bool Upgrade(SkillDataSO skill, float retryDelay, out bool learnedMissing)
        {
            learnedMissing = false;
            if (skill == null || IsEvolutionLocked(skill)) return false;

            if (!TryGet(skill, out var owned))
            {
                learnedMissing = true;
                skills.Add(new SkillRuntimeState(skill, 1));
                return true;
            }

            return UpgradeOwned(owned, retryDelay);
        }

        public bool Evolve(
            SkillDataSO source,
            SkillDataSO evolved,
            List<SkillCastType> removedSkillTypes,
            out string failureReason)
        {
            failureReason = null;
            removedSkillTypes?.Clear();

            if (source == null || evolved == null)
            {
                failureReason = "source or evolved skill is null.";
                return false;
            }

            if (!TryGet(source, out _))
            {
                failureReason = $"'{source.name}' not owned.";
                return false;
            }

            evolutionLockedSkills.Add(source);

            for (int i = skills.Count - 1; i >= 0; i--)
            {
                SkillDataSO ownedSkill = skills[i].Skill;
                if (ownedSkill != source) continue;

                removedSkillTypes?.Add(ownedSkill.castType);
                skills.RemoveAt(i);
            }

            skills.Add(new SkillRuntimeState(evolved, 1));
            return true;
        }

        public int GetSkillLevel(SkillDataSO skill)
        {
            return TryGet(skill, out var owned) ? owned.Level : 0;
        }

        public bool IsEvolutionLocked(SkillDataSO skill)
        {
            return skill != null && evolutionLockedSkills.Contains(skill);
        }

        public bool TryGet(SkillDataSO skill, out SkillRuntimeState ownedSkill)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].Skill != skill) continue;
                ownedSkill = skills[i];
                return true;
            }

            ownedSkill = null;
            return false;
        }

        public SkillSlotSyncData[] BuildSnapshot()
        {
            var slots = new SkillSlotSyncData[skills.Count];

            for (int i = 0; i < skills.Count; i++)
            {
                SkillDataSO skill = skills[i].Skill;
                slots[i] = new SkillSlotSyncData(
                    skill != null ? skill.skillName : "?",
                    skills[i].Level);
            }

            return slots;
        }

        private static bool UpgradeOwned(SkillRuntimeState owned, float retryDelay)
        {
            if (owned == null || !owned.TryLevelUp()) return false;
            owned.CooldownTimer = Mathf.Min(owned.CooldownTimer, retryDelay);
            return true;
        }
    }
}
