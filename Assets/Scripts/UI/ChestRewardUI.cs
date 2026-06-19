using UnityEngine;
using Vamsurlike.Items;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI
{
    // Stage 씬 Canvas에 배치 (항상 활성 유지 — OnEnable 구독 보장).
    // 상자 스킬 카드 선택 UI. 스킬 타입만 표시, 패시브 능력치 제외.
    public class ChestRewardUI : MonoBehaviour
    {
        [SerializeField] private GameObject  panel;
        [SerializeField] private ChestCardUI[] cards; // Inspector에서 3개 연결

        private ChestChoiceData[] currentOptions;

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

        private void Show(ChestChoiceData[] choices)
        {
            currentOptions = choices;
            if (panel != null) panel.SetActive(true);

            var upgradeCatalog = UpgradeCatalog.Instance;
            var recipeCatalog  = CombineRecipeCatalog.Instance;
            var itemCatalog    = ChestFallbackRewardCatalog.Instance;

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;

                if (i >= choices.Length)
                {
                    cards[i].gameObject.SetActive(false);
                    continue;
                }

                ChestChoiceData choice = choices[i];
                cards[i].gameObject.SetActive(true);
                int captured = i;

                if (choice.type == ChestChoiceType.Evolution)
                {
                    if (recipeCatalog == null || !recipeCatalog.IsValidIndex(choice.index))
                    {
                        cards[i].gameObject.SetActive(false);
                        continue;
                    }

                    cards[i].SetupEvolution(recipeCatalog.recipes[choice.index], () => OnCardSelected(captured));
                }
                else if (choice.type == ChestChoiceType.ItemReward)
                {
                    if (itemCatalog == null || !itemCatalog.IsValidIndex(choice.index))
                    {
                        cards[i].gameObject.SetActive(false);
                        continue;
                    }

                    cards[i].SetupItemReward(itemCatalog.rewards[choice.index], () => OnCardSelected(captured));
                }
                else
                {
                    if (upgradeCatalog == null || !upgradeCatalog.IsValidIndex(choice.index))
                    {
                        cards[i].gameObject.SetActive(false);
                        continue;
                    }

                    cards[i].SetupUpgrade(upgradeCatalog.options[choice.index], () => OnCardSelected(captured));
                }
            }
        }

        private void OnCardSelected(int cardIndex)
        {
            if (currentOptions == null || cardIndex >= currentOptions.Length) return;
            if (ChestRewardManager.Instance == null)
            {
                Debug.LogError($"[{nameof(ChestRewardUI)}] ChestRewardManager 인스턴스 없음");
                return;
            }

            ChestRewardManager.Instance.SubmitChoiceServerRpc(cardIndex);

            // 중복 선택 방지: 카드 숨김 — 패널은 서버 완료 이벤트에서 닫힘
            for (int i = 0; i < cards.Length; i++)
                if (cards[i] != null) cards[i].gameObject.SetActive(false);
        }

        private void Hide()
        {
            currentOptions = null;
            if (panel != null) panel.SetActive(false);
        }
    }
}
