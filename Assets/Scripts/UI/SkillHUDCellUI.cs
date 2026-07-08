using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // 스킬 HUD 한 칸. Inspector에서 연결.
    // 나중에 iconImage에 SkillDataSO.icon 스프라이트를 넣으면 됨.
    public class SkillHUDCellUI : MonoBehaviour
    {
        [FormerlySerializedAs("labelText")]
        [SerializeField] private TextMeshProUGUI skillText;
        [SerializeField] private Image iconImage;  // 아이콘 연결 시 활성화 예정
        [SerializeField] private string skillLabelFormat = "{0}\nLv.{1}";

        public void Set(string skillName, int level)
        {
            Set(new SkillHUDSlotViewData(skillName, level));
        }

        public void Set(SkillHUDSlotViewData slot)
        {
            if (skillText != null)
            {
                skillText.text = slot.IsMaxLevel
                    ? $"{slot.Name}\nLv.MAX"
                    : string.Format(skillLabelFormat, slot.Name, slot.Level);
            }
            if (iconImage != null) iconImage.gameObject.SetActive(false);
        }

        public void SetIcon(Sprite icon)
        {
            if (iconImage == null) return;
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);
        }
    }
}
