using TMPro;
using UnityEngine;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // Stage 씬 Canvas에 배치.
    // 보스 등장 전: 남은 시간 카운트다운
    // 보스 페이즈: bossLabel 표시
    public sealed class StageTimerUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private string          elapsedFormat   = "{0:00} : {1:00}";
        [SerializeField] private string          bossLabel       = "BOSS";

        private StageTimerViewModel viewModel;

        private void Awake()
        {
            viewModel = new StageTimerViewModel();
        }

        private void OnEnable()
        {
            viewModel.OnTimerUpdated += Render;
            viewModel.Bind();
            Render(viewModel.Last);
        }

        private void OnDisable()
        {
            viewModel.OnTimerUpdated -= Render;
            viewModel.Unbind();
        }

        private void OnDestroy() => viewModel?.Dispose();

        private void Render(StageTimerPayload p)
        {
            if (timerText == null) return;

            timerText.text = p.IsBossPhase
                ? bossLabel
                : FormatElapsedTime(p.ElapsedSeconds);
        }

        private string FormatElapsedTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int minutes      = totalSeconds / 60;
            int remainder    = totalSeconds % 60;

            return string.Format(elapsedFormat, minutes, remainder);
        }
    }
}
