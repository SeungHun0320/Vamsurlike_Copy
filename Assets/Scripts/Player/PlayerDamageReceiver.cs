using UnityEngine;
using Vamsurlike.Core;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerDamageReceiver : MonoBehaviour, IDamageable
    {
        private PlayerStats stats;

        public float HP      => stats != null ? stats.HP    : 0f;
        public float MaxHP   => stats != null ? stats.MaxHP : 0f;
        public bool  IsAlive => stats != null && stats.IsAlive;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            if (stats == null)
            {
                Debug.LogError($"[{nameof(PlayerDamageReceiver)}] PlayerStats not found.", this);
                enabled = false;
            }
        }

        public void TakeDamage(float amount) => stats?.TakeDamage(amount);
    }
}
