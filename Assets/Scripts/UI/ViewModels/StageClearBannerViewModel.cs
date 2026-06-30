using System;
using Vamsurlike.Stage;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.ViewModels
{
    public enum StageClearBannerDisplayMode
    {
        Hidden,
        Clear,
    }

    public readonly struct StageClearBannerViewState
    {
        public StageClearBannerViewState(StageClearBannerDisplayMode mode)
        {
            Mode = mode;
        }

        public StageClearBannerDisplayMode Mode { get; }
        public bool IsVisible => Mode == StageClearBannerDisplayMode.Clear;
        public bool BackdropVisible => IsVisible;
        public bool ContinueVisible => IsVisible;
    }

    public sealed class StageClearBannerViewModel : ViewModelBase
    {
        public event Action<StageClearBannerViewState> StateChanged;

        protected override void Subscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Flow.GameFlowChanged += HandleFlow;
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Flow.GameFlowChanged -= HandleFlow;
        }

        public void ContinueToResult()
        {
            StateChanged?.Invoke(new StageClearBannerViewState(StageClearBannerDisplayMode.Hidden));
            UIEventHub.Instance?.Flow.PublishStageResultRequested();
        }

        private void HandleFlow(GameFlowPayload payload)
        {
            StateChanged?.Invoke(payload.Next == GameFlowState.Clear
                ? new StageClearBannerViewState(StageClearBannerDisplayMode.Clear)
                : new StageClearBannerViewState(StageClearBannerDisplayMode.Hidden));
        }
    }
}
