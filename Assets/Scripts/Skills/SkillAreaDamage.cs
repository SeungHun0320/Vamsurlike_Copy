using UnityEngine;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 스킬 공통 광역 데미지 유틸. OverlapSphere 기반 스플래시를 중복 없이 공유.
    internal static class SkillAreaDamage
    {
        private static readonly Collider[] OverlapBuffer = new Collider[256];
        internal static void ApplySplash(
            Vector3 center,
            float radius,
            float damage,
            ulong ownerClientId,
            string skillTag)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(center, radius, OverlapBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col != null && col.TryGetComponent<EnemyNetworkBase>(out var enemy))
                    enemy.TakeDamage(damage, ownerClientId, skillTag);
                OverlapBuffer[i] = null;
            }
        }
    }
}
