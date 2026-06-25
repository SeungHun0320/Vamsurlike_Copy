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
        void ShowMelee(Vector3 position, Vector3 forward);
        void ShowGrenade(Vector3 from, Vector3 to, float arcHeight, float flightTime);
        void ShowGrenadeImpactCircle(Vector3 center, float radius, float duration);
        void RemoveSkillVisual(SkillCastType castType);
    }
}
