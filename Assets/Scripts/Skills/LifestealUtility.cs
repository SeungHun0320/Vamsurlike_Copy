using Unity.Netcode;
using Vamsurlike.Data;
using Vamsurlike.Player;

namespace Vamsurlike.Skills
{
    // 생명흡수탄 등 lifestealRatio > 0인 스킬 전용 공용 유틸 — 그 외 스킬은 0이라 항상 no-op.
    // NetworkProjectile(본체 타격)과 OrbitingProjectileMode(위성 타격) 양쪽에서 공유한다.
    internal static class LifestealUtility
    {
        internal static void Apply(SkillLevelData levelData, ulong ownerClientId, float damage)
        {
            float ratio = levelData != null ? levelData.lifestealRatio : 0f;
            if (ratio <= 0f) return;
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(ownerClientId, out var client)) return;
            if (client.PlayerObject == null) return;
            if (client.PlayerObject.TryGetComponent<PlayerNetworkStats>(out var stats))
                stats.Heal(damage * ratio);
        }
    }
}
