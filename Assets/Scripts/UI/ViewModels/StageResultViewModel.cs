using System;
using Vamsurlike.Stage;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.ViewModels
{
    public enum StageResultDisplayMode
    {
        Hidden,
        Result,
        GameOver,
    }

    public readonly struct StageResultViewState
    {
        public StageResultViewState(StageResultDisplayMode mode)
        {
            Mode = mode;
        }

        public StageResultDisplayMode Mode { get; }
        public bool IsVisible => Mode != StageResultDisplayMode.Hidden;
        public bool BackdropVisible => IsVisible;
        public bool StatsVisible => IsVisible;
    }

    public class StageResultViewModel : ViewModelBase
    {
        public event Action<StageResultViewState> StateChanged;
        public event Action<PlayerResultViewModel[]> OnPlayerResults;

        protected override void Subscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Flow.GameFlowChanged       += HandleFlow;
            UIEventHub.Instance.Flow.MatchResultReceived   += HandleResult;
            UIEventHub.Instance.Flow.StageResultRequested  += HandleResultRequested;
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Flow.GameFlowChanged       -= HandleFlow;
            UIEventHub.Instance.Flow.MatchResultReceived   -= HandleResult;
            UIEventHub.Instance.Flow.StageResultRequested  -= HandleResultRequested;
        }

        private void HandleFlow(GameFlowPayload payload)
        {
            if      (payload.Next == GameFlowState.GameOver) StateChanged?.Invoke(new StageResultViewState(StageResultDisplayMode.GameOver));
            else if (payload.Next != GameFlowState.Clear)    StateChanged?.Invoke(new StageResultViewState(StageResultDisplayMode.Hidden));
        }

        private void HandleResult(MatchResultPayload payload) =>
            OnPlayerResults?.Invoke(BuildVMs(payload.Entries));

        private void HandleResultRequested() =>
            StateChanged?.Invoke(new StageResultViewState(StageResultDisplayMode.Result));

        private static PlayerResultViewModel[] BuildVMs(MatchResultEntry[] entries)
        {
            if (entries == null) return System.Array.Empty<PlayerResultViewModel>();

            var vms = new PlayerResultViewModel[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                vms[i] = new PlayerResultViewModel
                {
                    PlayerName   = e.DisplayName.ToString(),
                    StatsSummary = $"Lv.{e.Level}  킬 {e.KillCount}  " +
                                   $"데미지 {e.TotalDamage:N0}  생존 {FormatTime(e.SurvivalTime)}",
                    Skills       = BuildSkillVMs(e),
                };
            }
            return vms;
        }

        private static SkillResultViewModel[] BuildSkillVMs(MatchResultEntry e)
        {
            var skills = new SkillResultViewModel[e.SkillEntryCount];
            for (int i = 0; i < e.SkillEntryCount; i++)
            {
                var raw = i switch { 0 => e.Skill0, 1 => e.Skill1, 2 => e.Skill2, _ => e.Skill3 };
                skills[i] = new SkillResultViewModel
                {
                    SkillName  = raw.SkillTag.ToString(),
                    DamageText = raw.Damage.ToString("N0"),
                };
            }
            return skills;
        }

        private static string FormatTime(float seconds)
        {
            int m = (int)(seconds / 60);
            int s = (int)(seconds % 60);
            return $"{m:D2}:{s:D2}";
        }
    }
}
