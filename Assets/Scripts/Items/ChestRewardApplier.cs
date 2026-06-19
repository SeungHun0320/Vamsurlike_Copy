using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Player;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Items
{
    internal sealed class ChestRewardApplier
    {
        public void Apply(GameObject playerObject, ChestChoiceData choice)
        {
            if (playerObject == null) return;

            switch (choice.type)
            {
                case ChestChoiceType.Evolution:
                    ApplyEvolution(playerObject, choice.index);
                    break;

                case ChestChoiceType.ItemReward:
                    ApplyItemReward(playerObject, choice.index);
                    break;

                default:
                    ApplyUpgrade(playerObject, choice.index);
                    break;
            }
        }

        private static void ApplyEvolution(GameObject playerObject, int recipeIndex)
        {
            var skillManager = playerObject.GetComponent<Skills.SkillManager>();
            if (skillManager != null)
                CombineSystem.TryEvolve(skillManager, recipeIndex);
        }

        private static void ApplyUpgrade(GameObject playerObject, int catalogIndex)
        {
            var catalog = UpgradeCatalog.Instance;
            if (catalog == null || !catalog.IsValidIndex(catalogIndex)) return;

            var handler = playerObject.GetComponent<PassiveStatHandler>();
            if (handler != null)
                handler.ApplyUpgrade(catalog.options[catalogIndex]);
        }

        private static void ApplyItemReward(GameObject playerObject, int itemIndex)
        {
            var catalog = ChestFallbackRewardCatalog.Instance;
            if (catalog == null || !catalog.IsValidIndex(itemIndex)) return;

            ItemDataSO item = catalog.rewards[itemIndex];
            switch (item.itemType)
            {
                case ItemType.HealthOrb:
                    playerObject.GetComponent<PlayerNetworkStats>()?.Heal(item.value);
                    break;

                case ItemType.Missile:
                    ApplyGlobalAttack(item.value);
                    break;
            }
        }

        private static void ApplyGlobalAttack(float damage)
        {
            IReadOnlyList<EnemyNetworkBase> enemies = EnemyRegistry.All;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyNetworkBase enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                enemy.TakeDamage(damage);
            }
        }
    }
}
