using UnityEngine;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerController : MonoBehaviour
    {
        private CharacterController m_cc;
        private PlayerInput         m_input;
        private PlayerStats         m_stats;

        private Vector3 m_vVelocity;
        private const float k_fGravity = -20f;

        private void Awake()
        {
            m_cc     = GetComponent<CharacterController>();
            m_input  = GetComponent<PlayerInput>();
            m_stats  = GetComponent<PlayerStats>();
        }

        private void Update()
        {
            if (!m_stats.IsAlive) return;
            Move();
        }

        private void Move()
        {
            Vector2 raw   = m_input.MoveInput;
            Vector3 dir   = new Vector3(raw.x, 0f, raw.y).normalized;
            float   speed = m_stats.MoveSpeed;

            // quarter-view: camera is at ~50° pitch, so world X/Z maps directly to input X/Y
            if (m_cc.isGrounded && m_vVelocity.y < 0f)
                m_vVelocity.y = -2f;

            m_vVelocity.y += k_fGravity * Time.deltaTime;

            Vector3 motion = dir * speed * Time.deltaTime + Vector3.up * m_vVelocity.y * Time.deltaTime;
            m_cc.Move(motion);

            if (dir != Vector3.zero)
                transform.forward = dir;
        }
    }
}
