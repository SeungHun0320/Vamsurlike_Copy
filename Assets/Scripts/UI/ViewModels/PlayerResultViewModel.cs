namespace Vamsurlike.UI.ViewModels
{
    public sealed class PlayerResultViewModel
    {
        public event System.Action Changed;

        public string       PlayerName;
        public string       StatsSummary;   // "Lv.5  킬 12  데미지 5,430  생존 03:45"
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

    public sealed class SkillResultViewModel
    {
        public string SkillName;
        public string DamageText;
    }
}
