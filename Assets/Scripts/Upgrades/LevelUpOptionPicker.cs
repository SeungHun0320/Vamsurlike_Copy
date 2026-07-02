using System;
using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Skills;

namespace Vamsurlike.Upgrades
{
    internal sealed class LevelUpOptionPicker
    {
        private readonly System.Random rng;

        public LevelUpOptionPicker(System.Random rng)
        {
            this.rng = rng;
        }

        public bool HasValidCatalog(UpgradeCatalog catalog)
        {
            if (catalog == null) return false;

            foreach (UpgradeOptionSO option in catalog.options)
                if (option != null)
                    return true;

            return false;
        }

        public int[] GenerateOptions(
            UpgradeCatalog catalog,
            int count,
            SkillManager skillManager,
            PassiveStatHandler passiveStatHandler,
            ulong clientId,
            Action<string> logWarning)
        {
            var pool = new List<int>(catalog.options.Length);
            for (int i = 0; i < catalog.options.Length; i++)
            {
                UpgradeOptionSO option = catalog.options[i];
                if (option == null) continue;
                if (IsEvolutionLockedSkillOption(option, skillManager)) continue;

                switch (option.effectType)
                {
                    case UpgradeEffectType.SkillLevelUp:
                        if (option.skillData == null) continue;
                        if (skillManager != null)
                        {
                            int level = skillManager.GetSkillLevel(option.skillData);
                            if (level <= 0 || level >= option.skillData.maxLevel) continue;
                        }
                        pool.Add(i);
                        break;

                    case UpgradeEffectType.NewSkill:
                        if (option.skillData == null) continue;
                        if (skillManager != null && skillManager.GetSkillLevel(option.skillData) > 0) continue;
                        pool.Add(i);
                        break;

                    default:
                        // 순수 패시브 — maxLevel(최대 픽 횟수)에 도달했으면 카드 풀에서 제외한다.
                        if (passiveStatHandler != null &&
                            passiveStatHandler.GetPassiveLevel(option.effectType) >= option.maxLevel) continue;
                        pool.Add(i);
                        break;
                }
            }

            if (pool.Count == 0)
            {
                logWarning?.Invoke($"clientId {clientId}: filtered option pool is empty. Falling back to whole catalog.");
                AddFallbackOptions(catalog, skillManager, pool);
            }

            count = Mathf.Min(count, pool.Count);
            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                int pick = rng.Next(pool.Count);
                result[i] = pool[pick];
                pool.RemoveAt(pick);
            }

            return result;
        }

        public int[] BuildCurrentLevels(
            int[] optionIndices,
            UpgradeCatalog catalog,
            SkillManager skillManager,
            PassiveStatHandler passiveStatHandler = null)
        {
            var levels = new int[optionIndices.Length];
            for (int i = 0; i < optionIndices.Length; i++)
            {
                if (!catalog.IsValidIndex(optionIndices[i])) continue;

                UpgradeOptionSO option = catalog.options[optionIndices[i]];
                if (option == null) continue;

                bool isSkillOption = option.skillData != null &&
                    (option.effectType == UpgradeEffectType.SkillLevelUp ||
                     option.effectType == UpgradeEffectType.NewSkill);

                if (isSkillOption)
                {
                    if (skillManager != null)
                        levels[i] = skillManager.GetSkillLevel(option.skillData);
                }
                else if (passiveStatHandler != null)
                {
                    levels[i] = passiveStatHandler.GetPassiveLevel(option.effectType);
                }
            }

            return levels;
        }

        private static void AddFallbackOptions(UpgradeCatalog catalog, SkillManager skillManager, List<int> pool)
        {
            for (int i = 0; i < catalog.options.Length; i++)
            {
                UpgradeOptionSO option = catalog.options[i];
                if (option != null && !IsEvolutionLockedSkillOption(option, skillManager))
                    pool.Add(i);
            }
        }

        private static bool IsEvolutionLockedSkillOption(UpgradeOptionSO option, SkillManager skillManager)
        {
            if (option == null || option.skillData == null || skillManager == null) return false;

            return (option.effectType == UpgradeEffectType.SkillLevelUp ||
                    option.effectType == UpgradeEffectType.NewSkill) &&
                   skillManager.IsEvolutionLocked(option.skillData);
        }
    }
}
