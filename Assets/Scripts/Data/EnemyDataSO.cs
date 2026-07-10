using UnityEngine;

namespace Vamsurlike.Data
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Vamsurlike/Data/Enemy")]
    public class EnemyDataSO : ScriptableObject
    {
        public string     enemyName = "Enemy";
        public GameObject prefab;

        [Header("Stats")]
        public float hp             = 50f;
        public float moveSpeed      = 3f;
        public float attackPower    = 10f;
        [Min(0f)] public float defense = 0f;
        public float attackRange    = 1.5f;
        public float attackInterval = 1f;
        public int   xpDrop         = 10;

        [Header("Drops")]
        public DropTableSO dropTable;

        [Header("UI")]
        public float floatingTextHeightOffset = 2f;

        [Header("Visual")]
        [Tooltip("프리팹을 재사용하는 색상 변형(팔레트 스왑)에 사용 — 렌더러 MaterialPropertyBlock에 적용된다.")]
        public Color tintColor = Color.white;
        [Tooltip("프리팹을 재사용하는 크기 변형 — transform.localScale에 적용된다 (콜라이더 크기도 함께 스케일됨).")]
        [Min(0.05f)] public float visualScale = 1f;
        [Tooltip("0=완전 무채색(회색조), 1=원본 색상 그대로. tintColor는 곱하기라 원본 텍스처가 다색이면 무채색을 만들 수 없어 별도 채도 조절이 필요하다.")]
        [Range(0f, 1f)] public float saturation = 1f;

        [Header("Flags")]
        public bool       isElite;
        public bool       isBoss;
        public GameObject bossMissilePrefab; // isBoss=true일 때 SpreadShot 패턴에서 사용
    }
}
