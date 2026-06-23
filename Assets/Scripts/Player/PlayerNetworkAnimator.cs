using UnityEngine;
using Vamsurlike.Network;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(PlayerNetworkController))]
    [RequireComponent(typeof(PlayerNetworkStats))]
    public class PlayerNetworkAnimator : ClientBehaviour
    {
        private static readonly int SpeedHash   = Animator.StringToHash("Speed");
        private static readonly int DieHash     = Animator.StringToHash("Die");
        private static readonly int ReviveHash  = Animator.StringToHash("Revive");

        private Animator animator;
        private PlayerNetworkController controller;
        private PlayerNetworkStats stats;

        private void Awake()
        {
            animator   = GetComponentInChildren<Animator>();
            controller = GetComponent<PlayerNetworkController>();
            stats      = GetComponent<PlayerNetworkStats>();
        }

        protected override void OnClientSpawned()
        {
            if (stats != null)
            {
                stats.HP.OnValueChanged     += OnHPChanged;
                stats.IsDowned.OnValueChanged += OnIsDownedChanged;
            }
        }

        protected override void OnClientDespawned()
        {
            if (stats != null)
            {
                stats.HP.OnValueChanged       -= OnHPChanged;
                stats.IsDowned.OnValueChanged -= OnIsDownedChanged;
            }
        }

        private void Update()
        {
            if (animator != null && controller != null)
                animator.SetFloat(SpeedHash, controller.NetSpeed.Value);
        }

        private void OnHPChanged(float prev, float current)
        {
            if (prev > 0f && current <= 0f && animator != null)
                animator.SetTrigger(DieHash);
        }

        private void OnIsDownedChanged(bool prev, bool current)
        {
            // 다운 상태에서 살아있는 상태로 복귀 = 부활
            if (prev && !current && stats.IsAlive && animator != null)
            {
                animator.ResetTrigger(DieHash);
                animator.SetTrigger(ReviveHash);
            }
        }
    }
}
