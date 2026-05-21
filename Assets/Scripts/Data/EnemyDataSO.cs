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
        public float defense        = 0f;
        public float attackRange    = 1.5f;
        public float attackInterval = 1f;
        public int   xpDrop         = 10;

        [Header("Flags")]
        public bool isElite;
        public bool isBoss;
    }
}
