using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Player
{
    public class PlayerFacade : MonoBehaviour, IPlayerFacade
    {
        private PlayerStats m_stats;

        public Vector3 Position => transform.position;
        public float   HP       => m_stats != null ? m_stats.HP    : 0f;
        public float   MaxHP    => m_stats != null ? m_stats.MaxHP : 0f;
        public bool    IsAlive  => m_stats != null && m_stats.IsAlive;

        private void Awake()
        {
            m_stats = GetComponent<PlayerStats>();
        }

        public void TakeDamage(float amount)  => m_stats?.TakeDamage(amount);
        public void Heal(float amount)         => m_stats?.Heal(amount);
        public float GetStat(StatType type)    => m_stats != null ? m_stats.GetStat(type) : 0f;
    }
}
