using UnityEngine;
using UnityEngine.InputSystem;

namespace Vamsurlike.Player
{
    public class PlayerInput : MonoBehaviour
    {
        public Vector2 MoveInput  { get; private set; }
        public bool    IsDashing  { get; private set; }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float h = 0f, v = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h = -1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h =  1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v = -1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v =  1f;

            MoveInput = new Vector2(h, v);
            IsDashing = kb.spaceKey.wasPressedThisFrame;
        }
    }
}
