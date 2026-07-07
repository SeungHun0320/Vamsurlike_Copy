using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // 수동 스킬(궁극기) 쿨다운 표시.
    // cooldownFill: Image(Filled, Radial360) — 쿨다운 진행 시각화
    // timerText: 쿨다운 중 남은 시간(초), 준비되면 keyHint 표시
    // readyGlow: 준비 완료 시 활성화되는 하이라이트 오브젝트
    public sealed class UltimateCooldownUI : MonoBehaviour
    {
        [SerializeField] private Image           cooldownFill;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private GameObject      readyGlow;
        [SerializeField] private string          keyHint = "SPACE";

        private UltimateCooldownViewModel viewModel;
        private string                    lastText;

        private void Awake()
        {
            viewModel = new UltimateCooldownViewModel();
        }

        private void OnEnable()
        {
            viewModel.OnCooldownChanged += Render;
            viewModel.Bind();
            Render(viewModel.Last);
        }

        private void OnDisable()
        {
            viewModel.OnCooldownChanged -= Render;
            viewModel.Unbind();
        }

        private void OnDestroy() => viewModel?.Dispose();

        private void Render(UltimateCooldownPayload p)
        {
            if (cooldownFill != null)
                cooldownFill.fillAmount = p.Duration > 0f
                    ? 1f - Mathf.Clamp01(p.Remaining / p.Duration)
                    : 1f;

            if (timerText != null)
            {
                string next = p.IsReady
                    ? keyHint
                    : Mathf.CeilToInt(p.Remaining).ToString();
                if (next != lastText)
                {
                    lastText = next;
                    timerText.text = next;
                }
            }

            if (readyGlow != null && readyGlow.activeSelf != p.IsReady)
                readyGlow.SetActive(p.IsReady);
        }
    }
}
