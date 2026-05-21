using UnityEngine;
using Vamsurlike.Core;
using Vamsurlike.Data;

namespace Vamsurlike.Enemy
{
    public class EnemyBase : MonoBehaviour, IDamageable
    {
        protected EnemyDataSO data;

        [Header("Runtime (read-only)")]
        [SerializeField] protected float currentHP;

        public float HP      => currentHP;
        public float MaxHP   => data != null ? data.hp : 0f;
        public bool  IsAlive => currentHP > 0f;

        public virtual void Initialize(EnemyDataSO enemyData)
        {
            data      = enemyData;
            currentHP = enemyData.hp;
        }

        public virtual void TakeDamage(float amount)
        {
            float dmg = Mathf.Max(0f, amount - data.defense);
            currentHP = Mathf.Max(0f, currentHP - dmg);
            Debug.Log($"[EnemyBase] {name} took {dmg:F1} dmg. HP: {currentHP:F1}/{data.hp:F1}");

            if (currentHP <= 0f) Die();
        }

        public virtual void Heal(float amount)
        {
            currentHP = Mathf.Min(MaxHP, currentHP + amount);
        }

        protected virtual void Die()
        {
            GameInstance.Instance?.World?.OnEnemyDied(this);
            Destroy(gameObject);
        }
    }
}
