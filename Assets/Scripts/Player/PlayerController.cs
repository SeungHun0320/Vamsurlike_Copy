using UnityEngine;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;

        private CharacterController cc;
        private PlayerInput         input;
        private PlayerStats         stats;

        private float verticalVelocity = -2f; // 첫 프레임부터 grounded 판정을 유지하는 초기값
        private const float Gravity = -20f;

        private void Awake()
        {
            cc    = GetComponent<CharacterController>();
            input = GetComponent<PlayerInput>();
            stats = GetComponent<PlayerStats>();

            if (mainCamera == null) 
                mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError($"[{nameof(PlayerController)}] Main Camera not found. Tag a camera 'MainCamera'.", this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (!stats.IsAlive)
                return;

            if (cc.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            else
                verticalVelocity += Gravity * Time.fixedDeltaTime;

            Move();
        }

        private void Move()
        {
            Vector2 raw = input.MoveInput;

            // 카메라 기준 XZ 기저벡터 (Y 성분 제거 후 정규화)
            Vector3 camForward = new(mainCamera.transform.forward.x, 0f, mainCamera.transform.forward.z);
            Vector3 camRight   = new(mainCamera.transform.right.x,   0f, mainCamera.transform.right.z);

            if (camForward.sqrMagnitude > 0.001f) camForward.Normalize();
            if (camRight.sqrMagnitude   > 0.001f) camRight.Normalize();

            // 축별 기여도 누적 → 마지막에 한 번 정규화 (대각 입력 속도 보정)
            Vector3 dir = camForward * raw.y + camRight * raw.x;
            if (dir.sqrMagnitude > 0.001f) dir.Normalize();

            Vector3 motion = dir * (stats.MoveSpeed * Time.fixedDeltaTime);
            motion.y = verticalVelocity * Time.fixedDeltaTime;
            cc.Move(motion);

            if (Vector3.zero != dir) transform.forward = dir;
        }
    }
}
