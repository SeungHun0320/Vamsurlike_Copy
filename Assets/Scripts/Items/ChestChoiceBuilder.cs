using System.Collections.Generic;
using Vamsurlike.Data;
using Vamsurlike.Skills;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Items
{
    internal sealed class ChestChoiceBuilder
    {
        private readonly System.Random rng;

        public ChestChoiceBuilder(System.Random rng)
        {
            this.rng = rng;
        }

        public ChestChoiceData[] Build(UpgradeCatalog catalog, SkillManager skillManager)
        {
            var choices = new List<ChestChoiceData>(3);

            List<ChestChoiceData> evolutionCards = CombineSystem.GetEvolutionCards(skillManager);
            AddRandomCards(choices, evolutionCards, 3);

            List<ChestChoiceData> skillCards = BuildSkillCards(catalog, skillManager);
            AddRandomCards(choices, skillCards, 3);

            List<ChestChoiceData> itemCards = BuildItemRewardCards();
            AddRandomCards(choices, itemCards, 3);

            return choices.ToArray();
        }

        private void AddRandomCards(List<ChestChoiceData> choices, List<ChestChoiceData> pool, int maxCount)
        {
            while (choices.Count < maxCount && pool.Count > 0)
            {
                int pick = rng.Next(pool.Count);
                choices.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
        }

        private static List<ChestChoiceData> BuildSkillCards(UpgradeCatalog catalog, SkillManager skillManager)
        {
            var pool = new List<ChestChoiceData>(catalog.options.Length);
            for (int i = 0; i < catalog.options.Length; i++)
            {
                UpgradeOptionSO option = catalog.options[i];
                if (option == null || option.skillData == null) continue;
                if (skillManager != null && skillManager.IsEvolutionLocked(option.skillData)) continue;

                switch (option.effectType)
                {
                    case UpgradeEffectType.SkillLevelUp:
                        if (skillManager != null)
                        {
                            int level = skillManager.GetSkillLevel(option.skillData);
                            if (level >= option.skillData.maxLevel) continue;
                        }
                        pool.Add(new ChestChoiceData(ChestChoiceType.UpgradeOption, i));
                        break;

                    case UpgradeEffectType.NewSkill:
                        if (skillManager != null && skillManager.GetSkillLevel(option.skillData) > 0) continue;
                        pool.Add(new ChestChoiceData(ChestChoiceType.UpgradeOption, i));
                        break;
                }
            }

            return pool;
        }

        private static List<ChestChoiceData> BuildItemRewardCards()
        {
            var catalog = ChestFallbackRewardCatalog.Instance;
            var pool = new List<ChestChoiceData>();
            if (catalog == null || catalog.rewards == null) return pool;

            for (int i = 0; i < catalog.rewards.Length; i++)
            {
                ItemDataSO item = catalog.rewards[i];
                if (item == null) continue;
                if (item.itemType != ItemType.HealthOrb && item.itemType != ItemType.Missile) continue;

                pool.Add(new ChestChoiceData(ChestChoiceType.ItemReward, i));
            }

            return pool;
        }
    }
}
