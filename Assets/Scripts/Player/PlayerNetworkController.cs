using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Vamsurlike.Network;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    public class PlayerNetworkController : ServerBehaviour
    {
        [SerializeField] private float fallbackMoveSpeed = 5f;
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float maxInputMagnitude = 1f;

        // 서버가 쓰고 모든 클라이언트가 읽어 애니메이션 속도를 동기화
        public NetworkVariable<float> NetSpeed { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private CharacterController characterController;
        private PlayerNetworkStats  stats;
        private Vector2             moveInput;
        private float               verticalVelocity;
        private bool                lastCanActState = true;

        public Vector2  MoveInput            => moveInput;
        public float    MoveInputMagnitude   => moveInput.magnitude;

        // MeleeNetworkSkill이 읽는 서버 전용 상태. 입력이 0이 돼도 마지막 이동 방향 유지.
        public Vector3  LastNonZeroMoveDirection { get; private set; } = Vector3.forward;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            stats = GetComponent<PlayerNetworkStats>();
        }

        private void FixedUpdate()
        {
            if (characterController == null) return;
            if (stats != null && !stats.CanAct)
            {
                moveInput = Vector2.zero;
                if (NetSpeed.Value != 0f)
                    NetSpeed.Value = 0f;
                return;
            }
            MoveServer(Time.fixedDeltaTime);
        }

        [ServerRpc]
        public void SubmitMoveInputServerRpc(Vector2 input)
        {
            bool canAct = stats == null || stats.CanAct;

            if (canAct != lastCanActState)
            {
                lastCanActState = canAct;
                if (!canAct)
                    Debug.LogWarning($"[SubmitMoveInputServerRpc] FAIL — CanAct=false (downed/dead). input={input}, owner={OwnerClientId}");
                else
                    Debug.Log($"[SubmitMoveInputServerRpc] OK — CanAct 복귀. owner={OwnerClientId}");
            }

            if (!canAct)
            {
                moveInput = Vector2.zero;
                return;
            }
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
                LastNonZeroMoveDirection = planar.normalized;
                Quaternion target = Quaternion.LookRotation(planar, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    target,
                    rotationSpeed * deltaTime);
            }

            // 모든 클라이언트가 Animator 속도를 동기화하도록 NetSpeed 갱신
            float newSpeed = moveInput.magnitude;
            if (Mathf.Abs(newSpeed - NetSpeed.Value) > 0.01f)
                NetSpeed.Value = newSpeed;
        }
    }
}
