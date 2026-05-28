using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    public interface ISkillExecutable
    {
        bool CanExecute(SkillDataSO skill);
        bool TryExecute(in SkillCastContext context);
    }
}
