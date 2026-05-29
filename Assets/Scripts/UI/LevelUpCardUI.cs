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
        [SerializeField] private Image           iconImage;
        [SerializeField] private Button          selectButton;

        public void Setup(UpgradeOptionSO option, Action onSelect)
        {
            if (option == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (nameText   != null)
            {
                nameText.text = option.upgradeName;
            } 
            if (descText   != null)
            {
                descText.text = option.description;
            }
            if (iconImage  != null)
            {
                iconImage.sprite = option.icon;
                iconImage.gameObject.SetActive(option.icon != null);
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => onSelect?.Invoke());
            }
        }
    }
}
