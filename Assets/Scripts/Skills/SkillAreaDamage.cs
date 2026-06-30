using UnityEngine;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 스킬 공통 광역 데미지 유틸. OverlapSphere 기반 스플래시를 중복 없이 공유.
    internal static class SkillAreaDamage
    {
        internal static void ApplySplash(
            Vector3 center,
            float radius,
            float damage,
            ulong ownerClientId,
            string skillTag)
        {
            var cols = Physics.OverlapSphere(center, radius);
            foreach (var col in cols)
            {
                if (col.TryGetComponent<EnemyNetworkBase>(out var enemy))
                    enemy.TakeDamage(damage, ownerClientId, skillTag);
            }
        }
    }
}
