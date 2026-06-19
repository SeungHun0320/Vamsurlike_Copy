using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Player;
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

        private void Show(int[] optionIndices, int[] currentLevels)
        {
            currentOptionIndices = optionIndices;
            var catalog = UpgradeCatalog.Instance;

            if (catalog == null)
            {
                Debug.LogError($"[{nameof(LevelUpUI)}] UpgradeCatalog을 찾을 수 없습니다.");
                return;
            }

            // 로컬 플레이어 스탯 — 패시브 수치 표시용
            NetworkObject localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var playerStats = localPlayer != null ? localPlayer.GetComponent<PlayerNetworkStats>() : null;
            var statHandler = localPlayer != null ? localPlayer.GetComponent<PassiveStatHandler>()  : null;

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
                var option = catalog.options[optionIndices[i]];
                float currentValue = ResolveCurrentValue(option, i, currentLevels, playerStats, statHandler);
                int captured = i;
                cards[i].Setup(option, currentValue, () => OnCardSelected(captured));
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
