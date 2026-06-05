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

        private int[] currentOptions;

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

        private void Show(int[] optionIndices)
        {
            currentOptions = optionIndices;
            if (panel != null) panel.SetActive(true);

            var catalog = UpgradeCatalog.Instance;

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;

                if (i >= optionIndices.Length || catalog == null || !catalog.IsValidIndex(optionIndices[i]))
                {
                    cards[i].gameObject.SetActive(false);
                    continue;
                }

                cards[i].gameObject.SetActive(true);
                int captured = i;
                cards[i].SetupUpgrade(catalog.options[optionIndices[i]], () => OnCardSelected(captured));
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
