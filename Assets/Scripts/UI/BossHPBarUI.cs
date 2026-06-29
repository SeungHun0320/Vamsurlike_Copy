using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // Stage 씬 Canvas에 배치. 보스 등장 시 표시, 처치/페이즈 종료 시 숨김.
    public sealed class BossHPBarUI : MonoBehaviour
    {
        [SerializeField] private GameObject      panel;
        [SerializeField] private Image           hpFill;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private string          hpFormat = "{0:0}/{1:0}";

        private BossHPBarViewModel viewModel;

        private void Awake()  { viewModel = new BossHPBarViewModel(); }
        private void Start()  { if (panel != null) panel.SetActive(false); }

        private void OnEnable()
        {
            viewModel.OnBossVisible += Show;
            viewModel.OnBossHidden  += Hide;
            viewModel.Bind();
        }

        private void OnDisable()
        {
            viewModel.OnBossVisible -= Show;
            viewModel.OnBossHidden  -= Hide;
            viewModel.Unbind();
        }

        private void Show(BossStatusPayload p)
        {
            if (panel != null) panel.SetActive(true);
            float maxHp = p.MaxHp > 0f ? p.MaxHp : 1f;
            if (hpFill != null) hpFill.fillAmount = p.Hp / maxHp;
            if (hpText != null) hpText.text       = string.Format(hpFormat, p.Hp, p.MaxHp);
        }

        private void Hide() { if (panel != null) panel.SetActive(false); }
    }
}
