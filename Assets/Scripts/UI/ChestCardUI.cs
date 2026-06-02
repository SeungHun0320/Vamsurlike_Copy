using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
            if (nameText != null) nameText.text = $"[진화] {evolvedName}";
            if (descText != null) descText.text  = $"{recipe.sourceSkill.skillName} → {evolvedName}";
            if (iconImage != null)
            {
                iconImage.sprite = recipe.evolvedSkill.icon;
                iconImage.gameObject.SetActive(recipe.evolvedSkill.icon != null);
            }
            if (evolutionBadge != null) evolutionBadge.SetActive(true);

            BindButton(onSelect);
        }

        private void BindButton(Action onSelect)
        {
            if (selectButton == null) return;
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect?.Invoke());
        }
    }
}
