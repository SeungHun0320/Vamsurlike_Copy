using UnityEngine;
using Vamsurlike.Items;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI
{
    // Stage 씬 Canvas에 배치 (항상 활성 유지 — OnEnable 구독 보장).
    // 상자 스킬 카드 선택 UI.
    public class ChestRewardUI : MonoBehaviour
    {
        [SerializeField] private GameObject   panel;
        [SerializeField] private ChestCardUI[] cards;

        private ChestChoiceData[]   currentOptions;
        private ChestRewardViewModel viewModel;

        private void Awake()
        {
            viewModel = new ChestRewardViewModel();
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

        private void Show(ChestOptionsPayload payload)
        {
            currentOptions = payload.Choices;
            if (panel != null) panel.SetActive(true);

            var upgradeCatalog = UpgradeCatalog.Instance;
            var recipeCatalog  = CombineRecipeCatalog.Instance;
            var itemCatalog    = ChestFallbackRewardCatalog.Instance;

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;

                if (i >= payload.Choices.Length)
                {
                    cards[i].gameObject.SetActive(false);
                    continue;
                }

                ChestChoiceData choice = payload.Choices[i];
                cards[i].gameObject.SetActive(true);
                int captured = i;

                if (choice.type == ChestChoiceType.Evolution)
                {
                    if (recipeCatalog == null || !recipeCatalog.IsValidIndex(choice.index))
                    { cards[i].gameObject.SetActive(false); continue; }
                    cards[i].SetupEvolution(recipeCatalog.recipes[choice.index], () => viewModel.SubmitChoice(captured));
                }
                else if (choice.type == ChestChoiceType.ItemReward)
                {
                    if (itemCatalog == null || !itemCatalog.IsValidIndex(choice.index))
                    { cards[i].gameObject.SetActive(false); continue; }
                    cards[i].SetupItemReward(itemCatalog.rewards[choice.index], () => viewModel.SubmitChoice(captured));
                }
                else
                {
                    if (upgradeCatalog == null || !upgradeCatalog.IsValidIndex(choice.index))
                    { cards[i].gameObject.SetActive(false); continue; }
                    cards[i].SetupUpgrade(upgradeCatalog.options[choice.index], () => viewModel.SubmitChoice(captured));
                }
            }
        }

        private void Hide()
        {
            currentOptions = null;
            if (panel != null) panel.SetActive(false);
        }
    }
}
