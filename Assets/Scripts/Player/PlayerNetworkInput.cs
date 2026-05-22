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

            Vector2 input = ReadMoveInput();
            controller.SubmitMoveInputServerRpc(input);
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
    }
}
