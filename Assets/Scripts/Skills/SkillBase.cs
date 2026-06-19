using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    // 순수 C# 추상 클래스 — NetworkBehaviour 없음. RPC/Coroutine은 context.Manager 경유.
    public abstract class SkillBase : ISkillExecutor
    {
        private float nextNoTargetLogTime;

        private const float NoTargetLogInterval = 2f;

        public abstract SkillCastType SupportedCastType { get; }
        public virtual bool IsPersistentExecution => false;

        public bool CanExecute(SkillDataSO skill) => skill != null && skill.castType == SupportedCastType;
        public abstract bool TryExecute(in SkillCastContext context);

        // SkillManager.Update()가 매 프레임 호출 — 클라이언트 비주얼 갱신용 (기본 no-op)
        public virtual void OnUpdate(Transform ownerTransform) { }

        // SkillManager.OnNetworkDespawn()이 호출 — 비주얼 정리용 (기본 no-op)
        public virtual void OnDespawn() { }

        // SkillManager 보유 목록에서 제거될 때 호출 — 지속 비주얼 정리용 (기본 no-op)
        public virtual void OnSkillRemoved(SkillCastType castType) { }

        protected bool ShouldLogNoTarget()
        {
            if (Time.time < nextNoTargetLogTime) return false;
            nextNoTargetLogTime = Time.time + NoTargetLogInterval;
            return true;
        }
    }
}
