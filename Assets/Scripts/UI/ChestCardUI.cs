using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.Data;
using Vamsurlike.Items;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI
{
    // 상자 선택 UI의 카드 1장. LevelUpCardUI와 달리 진화 카드와 일반 업그레이드 카드를 모두 처리.
    public class ChestCardUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private Image           iconImage;
        [SerializeField] private Button          selectButton;
        [SerializeField] private GameObject      evolutionBadge; // 진화 카드임을 표시하는 UI (선택 사항)
        [Header("Labels")]
        [SerializeField] private string evolutionNameFormat = "[진화] {0}";
        [SerializeField] private string evolutionDescriptionFormat = "{0} -> {1}";
        [SerializeField] private string healthRewardFormat = "HP {0:0} 회복";
        [SerializeField] private string missileRewardFormat = "모든 적에게 {0:0} 피해";
        [SerializeField] private string fallbackRewardDescription = "아이템 보상";

        public void SetupUpgrade(UpgradeOptionSO option, Action onSelect)
        {
            if (option == null) { gameObject.SetActive(false); return; }

            if (nameText  != null) nameText.text  = option.upgradeName;
            if (descText  != null) descText.text  = option.description;
            if (iconImage != null)
            {
                iconImage.sprite = option.icon;
                iconImage.gameObject.SetActive(option.icon != null);
            }
            if (evolutionBadge != null) evolutionBadge.SetActive(false);

            BindButton(onSelect);
        }

        public void SetupEvolution(CombineRecipeSO recipe, Action onSelect)
        {
            if (recipe == null || recipe.evolvedSkill == null) { gameObject.SetActive(false); return; }

            string evolvedName = recipe.evolvedSkill.skillName;
            string sourceName = recipe.sourceSkill != null ? recipe.sourceSkill.skillName : string.Empty;
            if (nameText != null) nameText.text = string.Format(evolutionNameFormat, evolvedName);
            if (descText != null) descText.text = string.Format(evolutionDescriptionFormat, sourceName, evolvedName);
            if (iconImage != null)
            {
                iconImage.sprite = recipe.evolvedSkill.icon;
                iconImage.gameObject.SetActive(recipe.evolvedSkill.icon != null);
            }
            if (evolutionBadge != null) evolutionBadge.SetActive(true);

            BindButton(onSelect);
        }

        public void SetupItemReward(ItemDataSO item, Action onSelect)
        {
            if (item == null) { gameObject.SetActive(false); return; }

            if (nameText != null) nameText.text = item.itemName;
            if (descText != null)
            {
                descText.text = item.itemType switch
                {
                    ItemType.HealthOrb => string.Format(healthRewardFormat, item.value),
                    ItemType.Missile => string.Format(missileRewardFormat, item.value),
                    _ => fallbackRewardDescription
                };
            }
            if (iconImage != null)
            {
                iconImage.sprite = item.icon;
                iconImage.gameObject.SetActive(item.icon != null);
            }
            if (evolutionBadge != null) evolutionBadge.SetActive(false);

            BindButton(onSelect);
        }

        private void BindButton(Action onSelect)
        {
            if (selectButton == null) return;
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect?.Invoke());
        }

        public void SetInteractable(bool interactable)
        {
            if (selectButton != null)
                selectButton.interactable = interactable;
        }
    }
}
