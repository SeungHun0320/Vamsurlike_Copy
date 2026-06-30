using TMPro;
using UnityEngine;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    public class PlayerSkillRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text skillNameText;
        [SerializeField] private TMP_Text damageText;

        public void Bind(SkillResultViewModel vm)
        {
            if (skillNameText != null) skillNameText.text = vm.SkillName;
            if (damageText    != null) damageText.text    = vm.DamageText;
        }
    }
}
