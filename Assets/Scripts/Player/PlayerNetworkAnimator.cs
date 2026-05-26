using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(PlayerNetworkController))]
    [RequireComponent(typeof(PlayerNetworkStats))]
    public class PlayerNetworkAnimator : NetworkBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int DieHash   = Animator.StringToHash("Die");

        private const float SpeedChangeTreshold = 0.01f;

        // 서버가 쓰고 모든 클라이언트가 읽어 로컬 Animator를 업데이트
        private readonly NetworkVariable<float> netSpeed = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Animator animator;
        private PlayerNetworkController controller;
        private PlayerNetworkStats stats;

        private void Awake()
        {
            animator   = GetComponentInChildren<Animator>();
            controller = GetComponent<PlayerNetworkController>();
            stats      = GetComponent<PlayerNetworkStats>();
        }

        public override void OnNetworkSpawn()
        {
            if (stats != null)
                stats.HP.OnValueChanged += OnHPChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (stats != null)
                stats.HP.OnValueChanged -= OnHPChanged;
        }

        private void Update()
        {
            if (IsServer && controller != null)
            {
                float newSpeed = controller.MoveInputMagnitude;
                if (Mathf.Abs(newSpeed - netSpeed.Value) > SpeedChangeTreshold)
                    netSpeed.Value = newSpeed;
            }

            if (animator != null)
                animator.SetFloat(SpeedHash, netSpeed.Value);
        }

        // HP NetworkVariable 변경은 모든 클라이언트에서 수신 → 로컬 Animator에 Die 트리거
        private void OnHPChanged(float prev, float current)
        {
            if (prev > 0f && current <= 0f && animator != null)
                animator.SetTrigger(DieHash);
        }
    }
}
