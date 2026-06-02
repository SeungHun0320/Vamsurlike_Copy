using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    public interface ISkillExecutor
    {
        SkillCastType SupportedCastType { get; }
        bool IsPersistentExecution { get; }
        bool CanExecute(SkillDataSO skill);
        bool TryExecute(in SkillCastContext context);
    }
}
