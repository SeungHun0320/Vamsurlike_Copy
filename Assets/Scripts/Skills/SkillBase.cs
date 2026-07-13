using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 순수 C# 추상 클래스 — NetworkBehaviour 없이 실행 포트만 사용한다.
    public abstract class SkillBase : ISkillExecutor
    {
        public abstract SkillCastType SupportedCastType { get; }
        public virtual bool IsPersistentExecution => false;

        public bool CanExecute(SkillDataSO skill) => skill != null && skill.castType == SupportedCastType;

        // 템플릿: 공통 유효성 검사 → 방향 결정 → Execute 호출.
        // 광역 처리 스킬(Aura, Melee 등)은 TryExecute를 직접 override.
        public virtual bool TryExecute(in SkillCastContext context)
        {
            if (context.Skill == null || context.LevelData == null
                || context.CasterTransform == null || context.CoroutineRunner == null)
                return false;

            Vector3 dir = AutoTargeting.ResolveDirection(
                context, context.CasterTransform.position, context.CasterForward, out var target);

            return Execute(context, dir, target);
        }

        // 타겟/방향 확정 후 실제 동작. 방향 기반 스킬이 override.
        protected virtual bool Execute(in SkillCastContext context, Vector3 direction, EnemyNetworkBase target)
            => false;

        // SkillManager.Update()가 매 프레임 호출 — 클라이언트 비주얼 갱신용 (기본 no-op)
        public virtual void OnUpdate(Transform ownerTransform) { }

        // SkillManager.OnNetworkDespawn()이 호출 — 비주얼 정리용 (기본 no-op)
        public virtual void OnDespawn() { }

        // SkillManager 보유 목록에서 제거될 때 호출 — 지속 비주얼 정리용 (기본 no-op)
        public virtual void OnSkillRemoved(SkillCastType castType) { }

    }
}
