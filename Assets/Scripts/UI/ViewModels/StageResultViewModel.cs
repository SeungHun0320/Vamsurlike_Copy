using System;
using UnityEngine;
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

            // 막대 그래프는 팀 내 최대치 대비 상대값이라, 먼저 지표별 최대치를 한 번 구해둔다.
            float maxDamage = 0f;
            int   maxKills  = 0, maxDowns = 0, maxDeaths = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.TotalDamage > maxDamage) maxDamage = e.TotalDamage;
                if (e.KillCount   > maxKills)  maxKills  = e.KillCount;
                if (e.DownCount   > maxDowns)  maxDowns  = e.DownCount;
                if (e.DeathCount  > maxDeaths) maxDeaths = e.DeathCount;
            }

            var vms = new PlayerResultViewModel[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                vms[i] = new PlayerResultViewModel
                {
                    PlayerName   = e.DisplayName.ToString(),
                    StatsSummary = $"Lv.{e.Level}  생존 {FormatTime(e.SurvivalTime)}  획득 {e.GoldEarned}G",
                    Bars         = BuildBarVMs(e, maxDamage, maxKills, maxDowns, maxDeaths),
                    Skills       = BuildSkillVMs(e),
                };
            }
            return vms;
        }

        private static StatBarViewModel[] BuildBarVMs(
            MatchResultEntry e, float maxDamage, int maxKills, int maxDowns, int maxDeaths)
        {
            return new[]
            {
                new StatBarViewModel { Label = "데미지", ValueText = e.TotalDamage.ToString("N0"), Fill = NormalizeFill(e.TotalDamage, maxDamage) },
                new StatBarViewModel { Label = "킬수",   ValueText = e.KillCount.ToString(),        Fill = NormalizeFill(e.KillCount, maxKills) },
                new StatBarViewModel { Label = "다운",   ValueText = e.DownCount.ToString(),         Fill = NormalizeFill(e.DownCount, maxDowns) },
                new StatBarViewModel { Label = "사망",   ValueText = e.DeathCount.ToString(),        Fill = NormalizeFill(e.DeathCount, maxDeaths) },
            };
        }

        // maxValue가 0이면(전원 0) 빈 막대로 표시 — 나눗셈으로 NaN이 나오지 않도록 방어
        private static float NormalizeFill(float value, float maxValue) =>
            maxValue > 0f ? Mathf.Clamp01(value / maxValue) : 0f;

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
