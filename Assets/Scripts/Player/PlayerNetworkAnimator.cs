using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Vamsurlike.Player
{
    public class PlayerNetworkAnimator : NetworkBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private Animator animator;
        private PlayerNetworkController controller;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            controller = GetComponent<PlayerNetworkController>();
        }

        private void Update()
        {
            if (!IsServer || animator == null || controller == null) return;
            animator.SetFloat(SpeedHash, controller.MoveInputMagnitude);
        }
    }
}
