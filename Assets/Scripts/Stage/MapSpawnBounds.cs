using UnityEngine;

namespace Vamsurlike.Stage
{
    // 스테이지 지형(Ground)에 부착해 맵 전체 스폰이 사용할 유효 범위를 명시적으로 선언한다.
    // WaveController의 mapSpawnHalfExtent 기본값은 Stage_01 크기(500x500)를 가정한 것이라
    // 새 스테이지를 추가할 때 값을 안 바꾸면 조용히 잘못된 범위로 스폰될 수 있다 — 스테이지별로 분리.
    public class MapSpawnBounds : MonoBehaviour
    {
        [Min(1f)]   public float halfExtent          = 220f;
        [Min(0.1f)] public float navMeshSampleRadius = 10f;
    }
}
