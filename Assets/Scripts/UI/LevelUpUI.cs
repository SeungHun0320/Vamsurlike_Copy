using UnityEngine;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI
{
    // Stage 씬의 Canvas에 배치 (IsLocalPlayer 기준으로 HUD와 함께 관리).
    // LevelUpManager의 정적 이벤트를 구독해 동작하므로 플레이어 오브젝트와 분리되어 있어도 됨.
    // Animator의 updateMode = AnimatorUpdateMode.UnscaledTime 으로 설정해야
    // Time.timeScale = 0 중에도 등장/퇴장 애니메이션이 재생됨.
    public class LevelUpUI : MonoBehaviour
    {
        [SerializeField] private GameObject    panel;
        [SerializeField] private LevelUpCardUI[] cards; // Inspector에서 카드 3개 연결

        private int[] currentOptionIndices;

        private void OnEnable()
        {
            LevelUpManager.OnOptionsReceived  += Show;
            LevelUpManager.OnLevelUpCompleted += Hide;
        }

        private void OnDisable()
        {
            LevelUpManager.OnOptionsReceived  -= Show;
            LevelUpManager.OnLevelUpCompleted -= Hide;
        }

        private void Show(int[] optionIndices)
        {
            currentOptionIndices = optionIndices;
            var catalog = UpgradeCatalog.Instance;

            if (catalog == null)
            {
                Debug.LogError($"[{nameof(LevelUpUI)}] UpgradeCatalog을 찾을 수 없습니다.");
                return;
            }

            if (panel != null) panel.SetActive(true);

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;

                bool hasOption = i < optionIndices.Length && catalog.IsValidIndex(optionIndices[i]);
                if (!hasOption)
                {
                    cards[i].gameObject.SetActive(false);
                    continue;
                }

                cards[i].gameObject.SetActive(true);
                int captured = i; // 클로저 캡처용
                cards[i].Setup(catalog.options[optionIndices[i]], () => OnCardSelected(captured));
            }
        }

        private void OnCardSelected(int cardIndex)
        {
            if (currentOptionIndices == null || cardIndex >= currentOptionIndices.Length) return;
            if (LevelUpManager.Instance == null)
            {
                Debug.LogError($"[{nameof(LevelUpUI)}] LevelUpManager 인스턴스 없음");
                return;
            }

            LevelUpManager.Instance.SubmitChoiceServerRpc(cardIndex);
            Hide();
        }

        private void Hide()
        {
            currentOptionIndices = null;
            if (panel != null) panel.SetActive(false);
        }
    }
}
