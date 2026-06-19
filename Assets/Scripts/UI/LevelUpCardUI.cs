using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI
{
    // LevelUpUI의 자식 카드 하나. Inspector에서 연결.
    public class LevelUpCardUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private TextMeshProUGUI levelText;  // 현재 레벨 / 현재 수치 표시
        [SerializeField] private Image           iconImage;
        [SerializeField] private Button          selectButton;

        // currentValue: 스킬 옵션이면 현재 레벨(정수), 패시브 옵션이면 현재 수치, -1이면 표시 없음
        public void Setup(UpgradeOptionSO option, float currentValue, Action onSelect)
        {
            if (option == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (nameText != null) nameText.text = option.upgradeName;
            if (descText != null) descText.text = option.description;

            if (iconImage != null)
            {
                iconImage.sprite = option.icon;
                iconImage.gameObject.SetActive(option.icon != null);
            }

            if (levelText != null)
            {
                string label = BuildLevelLabel(option, currentValue);
                levelText.text = label;
                levelText.gameObject.SetActive(label.Length > 0);
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => onSelect?.Invoke());
            }
        }

        private static string BuildLevelLabel(UpgradeOptionSO option, float currentValue)
        {
            if (currentValue < 0f) return string.Empty;

            switch (option.effectType)
            {
                case UpgradeEffectType.NewSkill:
                    return "NEW";
                case UpgradeEffectType.SkillLevelUp:
                    int lv = Mathf.RoundToInt(currentValue);
                    return $"Lv.{lv} → {lv + 1}";
                case UpgradeEffectType.PassiveMaxHP:
                    return $"현재 {currentValue:F0} HP";
                case UpgradeEffectType.PassiveMoveSpeed:
                    return $"현재 {currentValue:F1}";
                case UpgradeEffectType.PassiveAttackPower:
                    return $"현재 ×{currentValue:F2}";
                case UpgradeEffectType.PassivePickupRadius:
                    return $"현재 {currentValue:F1}m";
                default:
                    return string.Empty;
            }
        }
    }
}
