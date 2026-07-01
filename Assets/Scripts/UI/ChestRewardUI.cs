using UnityEngine;
using UnityEngine.UI;
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
        [SerializeField] private Image         timerFill;
        [SerializeField, Min(0f)] private float selectionTimeoutSeconds = 20f;

        private ChestRewardViewModel viewModel;
        private bool selectionTimerRunning;
        private float selectionTimerRemaining;

        private void Awake()
        {
            viewModel = new ChestRewardViewModel();
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
