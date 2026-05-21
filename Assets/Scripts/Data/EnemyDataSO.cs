using UnityEngine;

namespace Vamsurlike.Data
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Vamsurlike/Data/Enemy")]
    public class EnemyDataSO : ScriptableObject
    {
        public string     m_strEnemyName    = "Enemy";
        public GameObject m_goPrefab;

        [Header("Stats")]
        public float m_fHP              = 50f;
        public float m_fMoveSpeed       = 3f;
        public float m_fAttackPower     = 10f;
        public float m_fDefense         = 0f;
        public float m_fAttackRange     = 1.5f;
        public float m_fAttackInterval  = 1f;
        public int   m_iXPDrop          = 10;

        [Header("Flags")]
        public bool m_bIsElite;
        public bool m_bIsBoss;
    }
}
