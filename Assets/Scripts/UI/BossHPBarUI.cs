using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    public sealed class BossHPBarUI : MonoBehaviour
    {
        [SerializeField] private GameObject      panel;
        [SerializeField] private Image           hpFill;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private string          hpFormat = "{0:0}/{1:0}";

        private BossHPBarViewModel viewModel;
        private CanvasGroup panelCanvasGroup;

        private void Awake()
        {
            if (panel == null) panel = gameObject;
            panelCanvasGroup = panel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null) panelCanvasGroup = panel.AddComponent<CanvasGroup>();

            FilledImageUtility.ConfigureHorizontal(hpFill);
            viewModel = new BossHPBarViewModel();
            SetVisible(false);
        }

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
            SetVisible(true);
            float maxHp = p.MaxHp > 0f ? p.MaxHp : 1f;
            FilledImageUtility.SetAmount(hpFill, p.Hp / maxHp);
            if (hpText != null) hpText.text       = string.Format(hpFormat, p.Hp, p.MaxHp);
        }

        private void Hide() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (panel == null) return;

            if (!panel.activeSelf)
                panel.SetActive(true);

            if (panelCanvasGroup == null)
                panelCanvasGroup = panel.GetComponent<CanvasGroup>();

            if (panelCanvasGroup == null)
                return;

            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.interactable = visible;
            panelCanvasGroup.blocksRaycasts = visible;
        }
    }
}
