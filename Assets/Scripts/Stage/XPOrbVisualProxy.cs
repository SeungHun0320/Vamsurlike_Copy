using UnityEngine;

namespace Vamsurlike.Stage
{
    // NetworkObject가 아닌 로컬 비주얼 전용 컴포넌트.
    // XPOrbManager가 ClientRpc로 생성 시 ID를 주입한다.
    public class XPOrbVisualProxy : MonoBehaviour
    {
        public ulong OrbId { get; private set; }

        public void Initialize(ulong orbId)
        {
            OrbId = orbId;
        }
    }
}
