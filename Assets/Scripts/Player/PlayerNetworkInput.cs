using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(PlayerNetworkController))]
    public class PlayerNetworkInput : NetworkBehaviour
    {
        private PlayerNetworkController controller;

        private void Awake()
        {
            controller = GetComponent<PlayerNetworkController>();
        }

        private void FixedUpdate()
        {
            if (!IsOwner || controller == null) return;

            Vector2 raw = ReadMoveInput();
            Vector2 worldDir = ToCameraRelative(raw);
            controller.SubmitMoveInputServerRpc(worldDir);
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
