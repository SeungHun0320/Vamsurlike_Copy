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
        [Header("Labels")]
        [SerializeField] private string newSkillLabel = "NEW";
        [SerializeField] private string skillLevelFormat = "Lv.{0} -> {1}";
        [SerializeField] private string maxHpFormat = "현재 {0:F0} HP";
        [SerializeField] private string moveSpeedFormat = "현재 {0:F1}";
        [SerializeField] private string attackPowerFormat = "현재 x{0:F2}";
        [SerializeField] private string pickupRadiusFormat = "현재 {0:F1}m";

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

        private string BuildLevelLabel(UpgradeOptionSO option, float currentValue)
        {
            if (currentValue < 0f) return string.Empty;

            switch (option.effectType)
            {
                case UpgradeEffectType.NewSkill:
                    return newSkillLabel;
                case UpgradeEffectType.SkillLevelUp:
                    int lv = Mathf.RoundToInt(currentValue);
                    return string.Format(skillLevelFormat, lv, lv + 1);
                case UpgradeEffectType.PassiveMaxHP:
                    return string.Format(maxHpFormat, currentValue);
                case UpgradeEffectType.PassiveMoveSpeed:
                    return string.Format(moveSpeedFormat, currentValue);
                case UpgradeEffectType.PassiveAttackPower:
                    return string.Format(attackPowerFormat, currentValue);
                case UpgradeEffectType.PassivePickupRadius:
                    return string.Format(pickupRadiusFormat, currentValue);
                default:
                    return string.Empty;
            }
        }
    }
}
