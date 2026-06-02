using UnityEngine;
using Vamsurlike.Items;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI
{
    // Stage 씬 Canvas에 배치 (항상 활성 유지 — OnEnable 구독 보장).
    // ChestRewardManager의 정적 이벤트를 구독해 동작.
    // Animator가 있다면 updateMode = AnimatorUpdateMode.UnscaledTime 설정 필요.
    public class ChestRewardUI : MonoBehaviour
    {
        [SerializeField] private GameObject   panel;
        [SerializeField] private ChestCardUI[] cards; // Inspector에서 3개 연결

        private ChestChoiceData[] currentCards;

        private void OnEnable()
        {
            ChestRewardManager.OnOptionsReceived    += Show;
            ChestRewardManager.OnChestRewardCompleted += Hide;
        }

        private void OnDisable()
        {
            ChestRewardManager.OnOptionsReceived    -= Show;
            ChestRewardManager.OnChestRewardCompleted -= Hide;
        }

        private void Show(ChestChoiceData[] options)
        {
            currentCards = options;
            if (panel != null) panel.SetActive(true);

            var upgradeCatalog = UpgradeCatalog.Instance;
            var recipeCatalog  = CombineRecipeCatalog.Instance;

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;

                if (i >= options.Length)
                {
                    cards[i].gameObject.SetActive(false);
                    continue;
                }

                cards[i].gameObject.SetActive(true);
                ChestChoiceData choice  = options[i];
                int             captured = i;

                switch (choice.type)
                {
                    case ChestChoiceType.UpgradeOption:
                        if (upgradeCatalog != null && upgradeCatalog.IsValidIndex(choice.index))
                            cards[i].SetupUpgrade(upgradeCatalog.options[choice.index],
                                                  () => OnCardSelected(captured));
                        else
                            cards[i].gameObject.SetActive(false);
                        break;

                    case ChestChoiceType.Evolution:
                        if (recipeCatalog != null && recipeCatalog.IsValidIndex(choice.index))
                            cards[i].SetupEvolution(recipeCatalog.recipes[choice.index],
                                                    () => OnCardSelected(captured));
                        else
                            cards[i].gameObject.SetActive(false);
                        break;
                }
            }
        }

        private void OnCardSelected(int cardIndex)
        {
            if (currentCards == null || cardIndex >= currentCards.Length) return;
            if (ChestRewardManager.Instance == null)
            {
                Debug.LogError($"[{nameof(ChestRewardUI)}] ChestRewardManager 인스턴스 없음");
                return;
            }

            ChestRewardManager.Instance.SubmitChoiceServerRpc(cardIndex);
            Hide();
        }

        private void Hide()
        {
            currentCards = null;
            if (panel != null) panel.SetActive(false);
        }
    }
}
