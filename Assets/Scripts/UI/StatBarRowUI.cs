using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // StatBarRow 프리팹에 배치. Image.fillAmount(Horizontal)로 가로 막대 그래프를 표시한다.
    // PlayerResultRowUI가 스탯 항목(데미지/킬수/다운/사망)마다 하나씩 Instantiate한다.
    public class StatBarRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Image    fillImage;

        public void Bind(StatBarViewModel vm)
        {
            if (vm == null) return;
            if (labelText != null) labelText.text = vm.Label;
            if (valueText != null) valueText.text = vm.ValueText;
            if (fillImage != null) fillImage.fillAmount = vm.Fill;
        }
    }
}
