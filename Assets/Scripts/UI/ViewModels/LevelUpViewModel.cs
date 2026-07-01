using System;
using Unity.Netcode;
using Vamsurlike.Player;
using Vamsurlike.UI.Events;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI.ViewModels
{
    public readonly struct LevelUpCardViewData
    {
        public readonly bool            IsValid;
        public readonly UpgradeOptionSO Option;
        public readonly float           CurrentValue;

        public LevelUpCardViewData(UpgradeOptionSO option, float currentValue)
        { IsValid = true; Option = option; CurrentValue = currentValue; }
    }

    public class LevelUpViewModel : ViewModelBase
    {
        public event Action<LevelUpCardViewData[]> OnShow;
        public event Action                        OnHide;

        protected override void Subscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Reward.LevelUpOptionsReceived += HandleShow;
            UIEventHub.Instance.Reward.LevelUpCompleted       += HandleHide;
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Reward.LevelUpOptionsReceived -= HandleShow;
            UIEventHub.Instance.Reward.LevelUpCompleted       -= HandleHide;
        }

        private void HandleShow(LevelUpOptionsPayload payload)
        {
            var catalog = UpgradeCatalog.Instance;
            if (catalog == null) { OnHide?.Invoke(); return; }

            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var playerStats = localPlayer?.GetComponent<PlayerNetworkStats>();
            var statHandler = localPlayer?.GetComponent<PassiveStatHandler>();

            int count = payload.OptionIndices?.Length ?? 0;
            var cards = new LevelUpCardViewData[count];
            for (int i = 0; i < count; i++)
            {
                if (!catalog.IsValidIndex(payload.OptionIndices[i])) continue;
                var option = catalog.options[payload.OptionIndices[i]];
                float current = ResolveCurrentValue(option, i, payload.CurrentLevels, playerStats, statHandler);
                cards[i] = new LevelUpCardViewData(option, current);
            }
            OnShow?.Invoke(cards);
        }

        private static float ResolveCurrentValue(
            UpgradeOptionSO option, int cardIndex, int[] currentLevels,
            PlayerNetworkStats playerStats, PassiveStatHandler statHandler)
        {
            switch (option.effectType)
            {
                case UpgradeEffectType.SkillLevelUp:
                case UpgradeEffectType.NewSkill:
                    return currentLevels != null && cardIndex < currentLevels.Length ? currentLevels[cardIndex] : 0f;
                case UpgradeEffectType.PassiveMaxHP:
                    return playerStats != null ? playerStats.MaxHP.Value : -1f;
                case UpgradeEffectType.PassiveMoveSpeed:
                    return playerStats != null ? playerStats.MoveSpeed.Value : -1f;
                case UpgradeEffectType.PassivePickupRadius:
                    return playerStats != null ? playerStats.PickupRadius.Value : -1f;
                case UpgradeEffectType.PassiveAttackPower:
                    return statHandler != null ? statHandler.AttackMultiplier.Value : -1f;
                default:
                    return -1f;
            }
        }

        private void HandleHide() => OnHide?.Invoke();

        public void SubmitChoice(int cardIndex)
        {
            LevelUpManager.Instance?.SubmitChoiceServerRpc(cardIndex);
            OnHide?.Invoke();
        }

    }
}
