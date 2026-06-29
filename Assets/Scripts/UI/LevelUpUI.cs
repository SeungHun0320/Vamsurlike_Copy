using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Player;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI
{
    // Stage 씬의 Canvas에 배치.
    // Animator의 updateMode = AnimatorUpdateMode.UnscaledTime 으로 설정해야
    // Time.timeScale = 0 중에도 등장/퇴장 애니메이션이 재생됨.
    public class LevelUpUI : MonoBehaviour
    {
        [SerializeField] private GameObject     panel;
        [SerializeField] private LevelUpCardUI[] cards;

        private int[]            currentOptionIndices;
        private LevelUpViewModel viewModel;

        private void Awake()
        {
            viewModel = new LevelUpViewModel();
        }

        private void OnEnable()
        {
            viewModel.OnShow += Show;
            viewModel.OnHide += Hide;
            viewModel.Bind();
        }

        private void OnDisable()
        {
            viewModel.OnShow -= Show;
            viewModel.OnHide -= Hide;
            viewModel.Unbind();
        }

        private void Show(LevelUpOptionsPayload payload)
        {
            currentOptionIndices = payload.OptionIndices;
            var catalog = UpgradeCatalog.Instance;

            if (catalog == null)
            {
                Debug.LogError($"[{nameof(LevelUpUI)}] UpgradeCatalog을 찾을 수 없습니다.");
                return;
            }

            if (panel != null) panel.SetActive(true);

            // TODO: 플레이어 스탯 참조를 ViewModelBase 계층으로 이동 (Phase 8.2)
            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var playerStats = localPlayer != null ? localPlayer.GetComponent<PlayerNetworkStats>() : null;
            var statHandler = localPlayer != null ? localPlayer.GetComponent<PassiveStatHandler>()  : null;

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;

                bool hasOption = i < payload.OptionIndices.Length
                                 && catalog.IsValidIndex(payload.OptionIndices[i]);
                if (!hasOption)
                {
                    cards[i].gameObject.SetActive(false);
                    continue;
                }

                cards[i].gameObject.SetActive(true);
                var option = catalog.options[payload.OptionIndices[i]];
                float currentValue = ResolveCurrentValue(option, i, payload.CurrentLevels, playerStats, statHandler);
                int captured = i;
                cards[i].Setup(option, currentValue, () => viewModel.SubmitChoice(captured));
            }
        }

        private static float ResolveCurrentValue(
            UpgradeOptionSO option,
            int cardIndex,
            int[] currentLevels,
            PlayerNetworkStats playerStats,
            PassiveStatHandler statHandler)
        {
            switch (option.effectType)
            {
                case UpgradeEffectType.SkillLevelUp:
                case UpgradeEffectType.NewSkill:
                    return currentLevels != null && cardIndex < currentLevels.Length
                        ? currentLevels[cardIndex]
                        : 0f;
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

        private void Hide()
        {
            currentOptionIndices = null;
            if (panel != null) panel.SetActive(false);
        }
    }
}
