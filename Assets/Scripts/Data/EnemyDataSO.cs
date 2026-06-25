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

        [Header("Flags")]
        public bool       isElite;
        public bool       isBoss;
        public GameObject bossMissilePrefab; // isBoss=true일 때 SpreadShot 패턴에서 사용
    }
}
