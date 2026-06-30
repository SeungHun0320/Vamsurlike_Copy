using UnityEngine;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // Stage 씬의 Canvas에 배치.
    // Animator의 updateMode = AnimatorUpdateMode.UnscaledTime 으로 설정해야
    // Time.timeScale = 0 중에도 등장/퇴장 애니메이션이 재생됨.
    public class LevelUpUI : MonoBehaviour
    {
        [SerializeField] private GameObject      panel;
        [SerializeField] private LevelUpCardUI[] cards;

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

        private void OnDestroy() => viewModel?.Dispose();

        private void Show(LevelUpCardViewData[] cardData)
        {
            if (panel != null) panel.SetActive(true);

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;

                bool valid = i < cardData.Length && cardData[i].IsValid;
                cards[i].gameObject.SetActive(valid);
                if (!valid) continue;

                int captured = i;
                cards[i].Setup(cardData[i].Option, cardData[i].CurrentValue, () => viewModel.SubmitChoice(captured));
            }
        }

        private void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
