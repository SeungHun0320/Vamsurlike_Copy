using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vamsurlike.UI
{
    // 스킬 HUD 한 칸. Inspector에서 연결.
    // 나중에 iconImage에 SkillDataSO.icon 스프라이트를 넣으면 됨.
    public class SkillHUDCellUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private Image           iconImage;  // 아이콘 연결 시 활성화 예정

        public void Set(string skillName, int level)
        {
            if (labelText != null) labelText.text = $"{skillName}\nLv.{level}";
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
