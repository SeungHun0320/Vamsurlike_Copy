using UnityEngine;

namespace Vamsurlike.Player
{
    public class PlayerAnimator : MonoBehaviour
    {
        // ── Animator parameter hashes ──────────────────────────────────────
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int DieHash   = Animator.StringToHash("Die");

        // ── Shared state instances (stateless → 재사용) ───────────────────
        static readonly IdleState idleState = new();
        static readonly MoveState moveState = new();
        static readonly DieState  dieState  = new();

        // ── Components ────────────────────────────────────────────────────
        Animator    animator;
        PlayerInput input;
        PlayerStats stats;

        IAnimState currentState;

        // ── Unity ─────────────────────────────────────────────────────────

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            input    = GetComponent<PlayerInput>();
            stats    = GetComponent<PlayerStats>();
        }

        void Start() => ChangeState(idleState);

        void Update() => currentState?.Update(this);

        // ── State machine ─────────────────────────────────────────────────

        void ChangeState(IAnimState next)
        {
            currentState?.Exit(this);
            currentState = next;
            currentState?.Enter(this);
        }

        // ── State interface ───────────────────────────────────────────────

        interface IAnimState
        {
            void Enter(PlayerAnimator ctx);
            void Update(PlayerAnimator ctx);
            void Exit(PlayerAnimator ctx);
        }

        // ── Idle ──────────────────────────────────────────────────────────

        class IdleState : IAnimState
        {
            public void Enter(PlayerAnimator ctx)  => ctx.animator.SetFloat(SpeedHash, 0f);
            public void Exit(PlayerAnimator ctx)   { }
            public void Update(PlayerAnimator ctx)
            {
                if (!ctx.stats.IsAlive)
                    { ctx.ChangeState(dieState); return; }
                if (ctx.input.MoveInput.sqrMagnitude > 0.01f)
                    ctx.ChangeState(moveState);
            }
        }

        // ── Move ──────────────────────────────────────────────────────────

        class MoveState : IAnimState
        {
            public void Enter(PlayerAnimator ctx)  { }
            public void Exit(PlayerAnimator ctx)   { }
            public void Update(PlayerAnimator ctx)
            {
                if (!ctx.stats.IsAlive)
                    { ctx.ChangeState(dieState); return; }

                ctx.animator.SetFloat(SpeedHash, ctx.input.MoveInput.magnitude);

                if (ctx.input.MoveInput.sqrMagnitude <= 0.01f)
                    ctx.ChangeState(idleState);
            }
        }

        // ── Die ───────────────────────────────────────────────────────────

        class DieState : IAnimState
        {
            public void Enter(PlayerAnimator ctx)  => ctx.animator.SetTrigger(DieHash);
            public void Exit(PlayerAnimator ctx)   { }
            public void Update(PlayerAnimator ctx) { }
        }
    }
}
