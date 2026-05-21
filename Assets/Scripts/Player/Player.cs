using UnityEngine;
using Vamsurlike.Core;
using Vamsurlike.Data;

namespace Vamsurlike.Player
{
    public class Player : MonoBehaviour, IDamageable
    {
        private PlayerStats stats;

        public float HP      => stats != null ? stats.HP    : 0f;
        public float MaxHP   => stats != null ? stats.MaxHP : 0f;
        public bool  IsAlive => stats != null && stats.IsAlive;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
        }

        public void TakeDamage(float amount) => stats?.TakeDamage(amount);
        public void Heal(float amount)        => stats?.Heal(amount);

        public float GetStat(StatType type) => stats != null ? stats.GetStat(type) : 0f;
    }
}
