using UnityEngine;

namespace Vamsurlike.Stage
{
    // NetworkObject가 아닌 로컬 비주얼 전용 컴포넌트. XPOrbVisualProxy와 동일한 역할이지만
    // 골드는 XP와 책임(개인 귀속 vs 공유 풀)이 달라 별도 클래스로 유지한다.
    // GoldOrbManager가 ClientRpc로 생성 시 ID를 주입한다.
    public class GoldOrbVisualProxy : MonoBehaviour
    {
        public ulong OrbId { get; private set; }

        public void Initialize(ulong orbId)
        {
            OrbId = orbId;
        }

        public void Clear()
        {
            OrbId = 0;
        }
    }
}
