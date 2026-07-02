namespace Vamsurlike.UI.ViewModels
{
    public sealed class PlayerResultViewModel
    {
        public event System.Action Changed;

        public string       PlayerName;
        public string       StatsSummary;   // "Lv.5  생존 03:45" — 막대 그래프 대상이 아닌 보조 정보만
        public StatBarViewModel[]     Bars;   // 데미지/킬수/다운/사망 — 팀 내 최대치 대비 정규화된 가로 막대
        public SkillResultViewModel[] Skills;

        public bool IsExpanded { get; private set; }
        public bool CanExpand => SkillCount > 0;
        public int SkillCount => Skills?.Length ?? 0;

        public void ToggleExpanded()
        {
            SetExpanded(!IsExpanded);
        }

        public void SetExpanded(bool expanded)
        {
            bool next = expanded && CanExpand;
            if (IsExpanded == next) return;

            IsExpanded = next;
            Changed?.Invoke();
        }
    }

    // 팀원 간 비교가 의미 있는 지표를 가로 막대로 표시 (Phase 7.5)
    public sealed class StatBarViewModel
    {
        public string Label;     // "데미지", "킬수", "다운", "사망"
        public string ValueText; // "5,430"
        public float  Fill;      // 0~1, 이 스테이지에서 해당 지표의 팀 최대치 대비 비율
    }

    public sealed class SkillResultViewModel
    {
        public string SkillName;
        public string DamageText;
    }
}
