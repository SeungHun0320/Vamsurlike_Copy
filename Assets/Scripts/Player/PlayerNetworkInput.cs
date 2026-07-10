using UnityEngine;
using UnityEngine.InputSystem;
using Vamsurlike.Network;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(PlayerNetworkController))]
    [RequireComponent(typeof(Skills.SkillManager))]
    public class PlayerNetworkInput : OwnerBehaviour
    {
        // 카메라(Cinemachine Follow)가 매 프레임 미세하게 계속 움직이면 worldDir이 실수 오차만큼
        // 계속 달라져서, 정확히 같은 방향으로 계속 이동 중이어도 "값이 바뀌었다"고 오판해
        // 매 FixedUpdate마다 RPC를 보내게 된다 — 의미 있는 변화만 재전송하도록 임계값 비교로 교체.
        private const float DirectionChangeThresholdSqr = 0.0004f; // 0.02 단위 변화

        private PlayerNetworkController controller;
        private Skills.SkillManager     skillManager;
        private PlayerNetworkStats      playerStats;
        private Vector2                 lastSentDir;
        private bool                    forceNextSend;

        protected override void OnOwnerSpawned()
        {
            if (playerStats == null) return;
            playerStats.HP.OnValueChanged               += OnActionStateChanged;
            playerStats.IsDowned.OnValueChanged         += OnActionStateChanged;
            playerStats.IsDeadWaiting.OnValueChanged    += OnActionStateChanged;
            ForceNextMoveInputSend();
        }

        protected override void OnOwnerDespawned()
        {
            if (playerStats == null) return;
            playerStats.HP.OnValueChanged               -= OnActionStateChanged;
            playerStats.IsDowned.OnValueChanged         -= OnActionStateChanged;
            playerStats.IsDeadWaiting.OnValueChanged    -= OnActionStateChanged;
        }

        private void Awake()
        {
            controller   = GetComponent<PlayerNetworkController>();
            skillManager = GetComponent<Skills.SkillManager>();
            playerStats  = GetComponent<PlayerNetworkStats>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame && skillManager != null)
                skillManager.ActivateFirstManualSkillServerRpc();
        }

        private void FixedUpdate()
        {
            if (controller == null) return;

            Vector2 raw      = ReadMoveInput();
            Vector2 worldDir = ToCameraRelative(raw);

            if (!forceNextSend && (worldDir - lastSentDir).sqrMagnitude < DirectionChangeThresholdSqr) return;
            forceNextSend = false;
            lastSentDir = worldDir;
            controller.SubmitMoveInputServerRpc(worldDir);
        }

        private void OnActionStateChanged(float _, float __)
        {
            ForceNextMoveInputSend();
        }

        private void OnActionStateChanged(bool _, bool __)
        {
            ForceNextMoveInputSend();
        }

        private void ForceNextMoveInputSend()
        {
            forceNextSend = true;
        }

        private static Vector2 ReadMoveInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;

            Vector2 input = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;

            return Vector2.ClampMagnitude(input, 1f);
        }

        // 카메라 forward/right를 XZ 평면에 투영해 월드 방향으로 변환
        private static Vector2 ToCameraRelative(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f) return Vector2.zero;

            Camera cam = Camera.main;
            if (cam == null) return input;

            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return input;
            forward.Normalize();

            Vector3 right = cam.transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 worldDir = forward * input.y + right * input.x;
            return Vector2.ClampMagnitude(new Vector2(worldDir.x, worldDir.z), 1f);
        }
    }
}
