using System;
using Unity.Netcode;
using Vamsurlike.Data;
using Vamsurlike.Items;
using Vamsurlike.Player;
using Vamsurlike.UI.Events;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI.ViewModels
{
    public readonly struct ChestCardViewData
    {
        public readonly bool              IsValid;
        public readonly ChestChoiceType   Type;
        public readonly UpgradeOptionSO   Option;     // Type == UpgradeOption
        public readonly CombineRecipeSO   Recipe;     // Type == Evolution
        public readonly ItemDataSO        ItemReward; // Type == ItemReward

        private ChestCardViewData(ChestChoiceType type, UpgradeOptionSO option, CombineRecipeSO recipe, ItemDataSO item)
        { IsValid = true; Type = type; Option = option; Recipe = recipe; ItemReward = item; }

        public static ChestCardViewData ForUpgrade(UpgradeOptionSO option)
            => new(ChestChoiceType.UpgradeOption, option, null, null);
        public static ChestCardViewData ForEvolution(CombineRecipeSO recipe)
            => new(ChestChoiceType.Evolution, null, recipe, null);
        public static ChestCardViewData ForItemReward(ItemDataSO item)
            => new(ChestChoiceType.ItemReward, null, null, item);
    }

    public class ChestRewardViewModel : ViewModelBase
    {
        public event Action<ChestCardViewData[]> OnShow;
        public event Action                      OnHide;

        protected override void Subscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Reward.ChestOptionsReceived += HandleShow;
            UIEventHub.Instance.Reward.ChestRewardCompleted += HandleHide;
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Reward.ChestOptionsReceived -= HandleShow;
            UIEventHub.Instance.Reward.ChestRewardCompleted -= HandleHide;
        }

        private void HandleShow(ChestOptionsPayload payload)
        {
            if (!CanLocalPlayerChoose()) { OnHide?.Invoke(); return; }

            var upgradeCatalog = UpgradeCatalog.Instance;
            var recipeCatalog  = CombineRecipeCatalog.Instance;
            var itemCatalog    = ChestFallbackRewardCatalog.Instance;

            int count = payload.Choices?.Length ?? 0;
            var cards = new ChestCardViewData[count];
            for (int i = 0; i < count; i++)
            {
                ChestChoiceData choice = payload.Choices[i];
                if (choice.type == ChestChoiceType.Evolution)
                {
                    if (recipeCatalog != null && recipeCatalog.IsValidIndex(choice.index))
                        cards[i] = ChestCardViewData.ForEvolution(recipeCatalog.recipes[choice.index]);
                }
                else if (choice.type == ChestChoiceType.ItemReward)
                {
                    if (itemCatalog != null && itemCatalog.IsValidIndex(choice.index))
                        cards[i] = ChestCardViewData.ForItemReward(itemCatalog.rewards[choice.index]);
                }
                else
                {
                    if (upgradeCatalog != null && upgradeCatalog.IsValidIndex(choice.index))
                        cards[i] = ChestCardViewData.ForUpgrade(upgradeCatalog.options[choice.index]);
                }
            }
            OnShow?.Invoke(cards);
        }

        private void HandleHide() => OnHide?.Invoke();

        public void SubmitChoice(int cardIndex)
        {
            if (!CanLocalPlayerChoose()) return;
            ChestRewardManager.Instance?.SubmitChoiceServerRpc(cardIndex);
            OnHide?.Invoke();
        }

        private static bool CanLocalPlayerChoose()
        {
            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var stats = localPlayer?.GetComponent<PlayerNetworkStats>();
            return stats != null && stats.CanAct;
        }
    }
}
