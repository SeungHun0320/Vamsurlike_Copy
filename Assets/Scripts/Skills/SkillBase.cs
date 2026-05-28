using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    public abstract class SkillBase : NetworkBehaviour, ISkillExecutable
    {
        [SerializeField] private float noTargetLogInterval = 2f;

        private float nextNoTargetLogTime;

        protected abstract SkillCastType SupportedCastType { get; }

        // 지속형 스킬(Aura/Orbital)은 true로 오버라이드 — SkillManager가 tickInterval/duration 방식으로 실행
        public virtual bool IsPersistentExecution => false;

        public bool CanExecute(SkillDataSO skill)
        {
            return skill != null && skill.castType == SupportedCastType;
        }

        public abstract bool TryExecute(in SkillCastContext context);

        protected bool ShouldLogNoTarget()
        {
            if (Time.time < nextNoTargetLogTime)
                return false;

            nextNoTargetLogTime = Time.time + noTargetLogInterval;
            return true;
        }
    }
}
