using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Player
{
    // 플레이어 색상 동기화. 서버가 쓰고 모든 클라이언트가 렌더러에 적용.
    // Mesh Object 하위의 모든 MeshRenderer를 대상으로 material.color 를 변경한다.
    public class PlayerColorSync : NetworkBehaviour
    {
        public static readonly Color32[] Palette =
        {
            new(220,  60,  60, 255), // 빨강
            new( 60, 120, 220, 255), // 파랑
            new( 60, 200,  80, 255), // 초록
            new(220, 180,  40, 255), // 노랑
            new(160,  60, 220, 255), // 보라
            new(220, 120,  40, 255), // 주황
            new( 40, 200, 200, 255), // 청록
            new(220,  80, 160, 255), // 분홍
            new(255, 255, 255, 255), // 하양
        };

        public readonly NetworkVariable<int> ColorIndex = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private MeshRenderer[] renderers;

        private void Awake()
        {
            renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
        }

        public override void OnNetworkSpawn()
        {
            // 람다를 매번 새로 만들면 -=가 다른 인스턴스라 실제로 구독 해제가 안 된다 —
            // 반드시 같은 델리게이트(이름 있는 메서드)로 구독/해제해야 한다.
            ColorIndex.OnValueChanged += OnColorIndexChanged;
            ApplyColor(ColorIndex.Value);
        }

        public override void OnNetworkDespawn()
        {
            ColorIndex.OnValueChanged -= OnColorIndexChanged;
        }

        private void OnColorIndexChanged(int _, int next) => ApplyColor(next);

        // 서버 전용: 색상 인덱스 설정
        public void SetColorIndex(int index)
        {
            if (!IsServer) return;
            ColorIndex.Value = Mathf.Clamp(index, 0, Palette.Length - 1);
        }

        private void ApplyColor(int index)
        {
            var color = (Color)Palette[Mathf.Clamp(index, 0, Palette.Length - 1)];
            foreach (var r in renderers)
            {
                if (r != null)
                    r.material.color = color;
            }
        }
    }
}
