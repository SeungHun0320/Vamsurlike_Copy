using UnityEngine;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 망치류(사각형 판정) 공통 히트 판정 유틸. Melee/Earthshatter가 공유한다.
    // 원뿔(샷건)과 판정 형태를 구분하기 위해 전방 사각형 박스 안의 적만 타격한다.
    internal static class MeleeBoxHit
    {
        private const float MinToEnemySqrMagnitude = 0.0001f;
        // 적 콜라이더 피벗 높이 편차를 넉넉히 포괄하는 판정 박스 높이(수직 방향은 사실상 무제한 취급).
        private const float BoxHeight = 6f;
        private static readonly int EnemyLayerMask = 1 << 7; // Layer 7: Enemy
        private static readonly Collider[] OverlapBuffer = new Collider[64];

        // origin 기준 forward 방향으로 뻗은 전방 사각형(세로 range, 가로 width) 안의 모든 적에게 데미지.
        // Physics.OverlapBox로 회전된 사각형 판정을 한 번에 처리한다(각도 비교 없음).
        // onHit이 있으면 데미지 적용 직후 적마다 호출(대지분쇄자의 기절 적용 등). 맞은 적 수를 반환.
        internal static int Apply(
            Vector3 origin, Vector3 forward, float range, float width,
            float damage, ulong ownerClientId, string skillTag,
            System.Action<EnemyNetworkBase> onHit = null)
        {
            Vector3 flatForward = FlattenForward(forward);
            Quaternion rotation = Quaternion.LookRotation(flatForward, Vector3.up);
            Vector3 halfExtents = new(width * 0.5f, BoxHeight * 0.5f, range * 0.5f);
            Vector3 center = origin + flatForward * (range * 0.5f);

            int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, OverlapBuffer, rotation, EnemyLayerMask);

            int count = 0;
            for (int i = 0; i < hitCount; i++)
            {
                if (!OverlapBuffer[i].TryGetComponent<EnemyNetworkBase>(out var enemy)) continue;

                enemy.TakeDamage(damage, ownerClientId, skillTag);
                onHit?.Invoke(enemy);
                count++;
            }

            return count;
        }

        internal static Vector3 FlattenForward(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude > MinToEnemySqrMagnitude ? forward.normalized : Vector3.forward;
        }

        internal static Vector3 GetRight(Vector3 flatForward) => Vector3.Cross(Vector3.up, flatForward).normalized;
    }
}
