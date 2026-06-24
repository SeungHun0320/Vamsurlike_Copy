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
        private bool isShowingDownedState;

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
                stats.HP.OnValueChanged       += OnHPChanged;
                stats.IsDowned.OnValueChanged += OnIsDownedChanged;
                SyncLifeAnimation(true);
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

        private void OnHPChanged(float _, float __)
        {
            SyncLifeAnimation(false);
        }

        private void OnIsDownedChanged(bool _, bool __)
        {
            SyncLifeAnimation(false);
        }

        // HP와 IsDowned는 별도 NetworkVariable이므로 어느 콜백이 먼저 와도
        // 두 값을 합친 현재 상태만 보고 애니메이션을 전환한다.
        private void SyncLifeAnimation(bool force)
        {
            if (animator == null || stats == null) return;

            bool shouldShowDowned = stats.IsDowned.Value || !stats.IsAlive;
            if (!force && shouldShowDowned == isShowingDownedState) return;

            isShowingDownedState = shouldShowDowned;
            if (shouldShowDowned)
            {
                animator.ResetTrigger(ReviveHash);
                animator.SetTrigger(DieHash);
            }
            else
            {
                animator.ResetTrigger(DieHash);
                if (!force)
                    animator.SetTrigger(ReviveHash);
            }
        }
    }
}
