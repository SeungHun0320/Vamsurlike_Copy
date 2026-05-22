using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    public class PlayerNetworkController : NetworkBehaviour
    {
        [SerializeField] private float fallbackMoveSpeed = 5f;
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float maxInputMagnitude = 1f;

        private CharacterController characterController;
        private PlayerNetworkStats stats;
        private Vector2 moveInput;
        private float verticalVelocity;

        public Vector2 MoveInput => moveInput;
        public float MoveInputMagnitude => moveInput.magnitude;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            stats = GetComponent<PlayerNetworkStats>();
        }

        private void FixedUpdate()
        {
            if (!IsServer || characterController == null) return;
            MoveServer(Time.fixedDeltaTime);
        }

        [ServerRpc]
        public void SubmitMoveInputServerRpc(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, maxInputMagnitude);
        }

        private void MoveServer(float deltaTime)
        {
            float speed = stats != null ? stats.MoveSpeed.Value : fallbackMoveSpeed;
            Vector3 planar = new Vector3(moveInput.x, 0f, moveInput.y);

            if (planar.sqrMagnitude > 1f)
                planar.Normalize();

            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            verticalVelocity += gravity * deltaTime;

            Vector3 motion = planar * speed;
            motion.y = verticalVelocity;
            characterController.Move(motion * deltaTime);

            if (planar.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(planar, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    target,
                    rotationSpeed * deltaTime);
            }
        }
    }
}
