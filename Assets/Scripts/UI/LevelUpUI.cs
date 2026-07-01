using UnityEngine;
using UnityEngine.UI;
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
        [SerializeField] private Image           timerFill;
        [SerializeField, Min(0f)] private float selectionTimeoutSeconds = 20f;

        private LevelUpViewModel viewModel;
        private bool selectionTimerRunning;
        private float selectionTimerRemaining;

        private void Awake()
        {
            viewModel = new LevelUpViewModel();
        }

        private void Update()
        {
            if (!selectionTimerRunning) return;

            selectionTimerRemaining = Mathf.Max(0f, selectionTimerRemaining - Time.unscaledDeltaTime);
            RefreshTimerFill();

            if (selectionTimerRemaining <= 0f)
                selectionTimerRunning = false;
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

            BeginSelectionCooldown();
        }

        private void Hide()
        {
            selectionTimerRunning = false;
            if (timerFill != null) timerFill.transform.parent.gameObject.SetActive(false);
            if (panel != null) panel.SetActive(false);
        }

        private void BeginSelectionCooldown()
        {
            selectionTimerRunning = selectionTimeoutSeconds > 0f;
            selectionTimerRemaining = selectionTimeoutSeconds;
            SetCardsInteractable(true);

            if (selectionTimerRunning)
            {
                if (timerFill != null) timerFill.transform.parent.gameObject.SetActive(true);
                RefreshTimerFill();
            }
            else if (timerFill != null)
            {
                timerFill.transform.parent.gameObject.SetActive(false);
            }
        }

        private void SetCardsInteractable(bool interactable)
        {
            foreach (var card in cards)
            {
                if (card != null && card.gameObject.activeSelf)
                    card.SetInteractable(interactable);
            }
        }

        private void RefreshTimerFill()
        {
            if (timerFill != null)
                timerFill.fillAmount = selectionTimeoutSeconds > 0f
                    ? Mathf.Clamp01(selectionTimerRemaining / selectionTimeoutSeconds)
                    : 0f;
        }
    }
}
