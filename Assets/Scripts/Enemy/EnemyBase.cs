using UnityEngine;
using Vamsurlike.Core;
using Vamsurlike.Data;

namespace Vamsurlike.Enemy
{
    public class EnemyBase : MonoBehaviour
    {
        protected EnemyDataSO m_data;

        [Header("Runtime (read-only)")]
        [SerializeField] protected float m_fHP;

        public bool IsAlive => m_fHP > 0f;

        public virtual void Initialize(EnemyDataSO data)
        {
            m_data = data;
            m_fHP  = data.m_fHP;
        }

        public virtual void TakeDamage(float raw)
        {
            float dmg = Mathf.Max(0f, raw - m_data.m_fDefense);
            m_fHP = Mathf.Max(0f, m_fHP - dmg);
            Debug.Log($"[EnemyBase] {name} took {dmg:F1} dmg. HP: {m_fHP:F1}/{m_data.m_fHP:F1}");

            if (m_fHP <= 0f) Die();
        }

        protected virtual void Die()
        {
            GameInstance.Instance?.World?.OnEnemyDied(this);
            Destroy(gameObject);
        }
    }
}
