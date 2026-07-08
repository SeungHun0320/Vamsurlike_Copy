using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    public interface ISkillVFXBroadcaster
    {
        void ShowOrbital(int count, float radius, float rotationSpeed);
        void ShowBlackHole(Vector3 center, float duration, float radius);
        void ShowAreaCircle(SkillCastType castType, float radius, float duration, bool followOwner);
        void ShowUltimate(Vector3 position);
        void ShowMelee(SkillCastType castType, Vector3 position, Vector3 forward, float range, float arcAngle);
        // 망치류(사각형 판정) 전용 — 원뿔(ShowMelee)과 달리 전방 사각형 범위를 표시한다.
        void ShowMeleeBox(SkillCastType castType, Vector3 position, Vector3 forward, float range, float width);
        void ShowGrenade(Vector3 from, Vector3 to, float arcHeight, float flightTime);
        void ShowGrenadeImpactCircle(Vector3 center, float radius, float duration);
        // 왕복 관통창 귀환 연출 전용 — 고정 좌표가 아니라 시전자(이 컴포넌트의 소유자)의 실시간 위치를 계속 추적한다.
        void ShowHomingReturn(Vector3 from, float speed);
        void RemoveSkillVisual(SkillCastType castType);
    }
}
