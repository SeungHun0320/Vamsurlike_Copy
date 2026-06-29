using UnityEngine;
using Vamsurlike.Items;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // Stage 씬 Canvas에 배치 (항상 활성 유지 — OnEnable 구독 보장).
    // 상자 스킬 카드 선택 UI.
    public class ChestRewardUI : MonoBehaviour
    {
        [SerializeField] private GameObject    panel;
        [SerializeField] private ChestCardUI[] cards;

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

        private void Show(ChestCardViewData[] cardData)
        {
            if (panel != null) panel.SetActive(true);

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;

                bool valid = i < cardData.Length && cardData[i].IsValid;
                cards[i].gameObject.SetActive(valid);
                if (!valid) continue;

                int captured = i;
                ChestCardViewData d = cardData[i];

                if (d.Type == ChestChoiceType.Evolution)
                    cards[i].SetupEvolution(d.Recipe, () => viewModel.SubmitChoice(captured));
                else if (d.Type == ChestChoiceType.ItemReward)
                    cards[i].SetupItemReward(d.ItemReward, () => viewModel.SubmitChoice(captured));
                else
                    cards[i].SetupUpgrade(d.Option, () => viewModel.SubmitChoice(captured));
            }
        }

        private void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
