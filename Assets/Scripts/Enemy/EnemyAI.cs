using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Vamsurlike.Data;
using Vamsurlike.Network;
using Vamsurlike.Player;
using Vamsurlike.Stage;

namespace Vamsurlike.Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyNetworkBase))]
    public class EnemyAI : ServerBehaviour
    {
        private static readonly int SpeedHash  = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        internal NavMeshAgent     Agent  { get; private set; }
        internal EnemyNetworkBase Base   { get; private set; }
        internal Animator         Anim   { get; private set; }
        internal Transform        Target { get; private set; }

        private IEnemyState currentState;
        private float targetUpdateTimer;
        private const float TargetUpdateInterval = 0.5f;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Base  = GetComponent<EnemyNetworkBase>();
            Anim  = GetComponentInChildren<Animator>();
            // 클라이언트에서 NavMeshAgent가 활성화된 채로 남으면 NavMesh 오류 발생.
            // OnServerSpawned()에서 서버에서만 다시 켠다.
            Agent.enabled = false;
        }

        protected override void OnServerSpawned()
        {
            Agent.enabled = true;
            ChangeState(EnemyStates.Idle);
        }

        internal void ApplyData(EnemyDataSO data)
        {
            if (data == null) return;
            Agent.speed            = data.moveSpeed;
            Agent.stoppingDistance = Mathf.Max(0.1f, data.attackRange * 0.8f);
        }

        private void Update()
        {
            if (!Base.IsAlive) return;
            if (StageRuntime.Instance == null || StageRuntime.Instance.CurrentState.Value != GameState.Playing) return;

            targetUpdateTimer -= Time.deltaTime;
            if (targetUpdateTimer <= 0f)
            {
                targetUpdateTimer = TargetUpdateInterval;
                RefreshTarget();
            }

            currentState?.Update(this);

            if (Anim != null)
                Anim.SetFloat(SpeedHash, Agent.velocity.magnitude);
        }

        internal void TriggerAttackAnim()
        {
            if (Anim != null) Anim.SetTrigger(AttackHash);
        }

        internal void ChangeState(IEnemyState next)
        {
            currentState?.Exit(this);
            currentState = next;
            currentState.Enter(this);
        }

        private void RefreshTarget()
        {
            float     closest       = float.MaxValue;
            Transform bestTransform = null;

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                var playerObj = client.PlayerObject;
                if (playerObj == null) continue;

                var stats = playerObj.GetComponent<PlayerNetworkStats>();
                if (stats != null && !stats.IsAlive) continue;

                float sqrDist = Vector3.SqrMagnitude(transform.position - playerObj.transform.position);
                if (sqrDist < closest) { closest = sqrDist; bestTransform = playerObj.transform; }
            }

            Target = bestTransform;
        }
    }

    // ─── State Interface ───────────────────────────────────────────────────────

    internal interface IEnemyState
    {
        void Enter(EnemyAI ai);
        void Update(EnemyAI ai);
        void Exit(EnemyAI ai);
    }

    // ─── State 싱글턴 (무상태 — GC 방지) ──────────────────────────────────────

    internal static class EnemyStates
    {
        internal static readonly EnemyIdleState  Idle  = new();
        internal static readonly EnemyChaseState Chase = new();
    }

    // ─── Idle ──────────────────────────────────────────────────────────────────

    internal sealed class EnemyIdleState : IEnemyState
    {
        public void Enter(EnemyAI ai)
        {
            if (ai.Agent.isOnNavMesh) ai.Agent.ResetPath();
        }

        public void Update(EnemyAI ai)
        {
            if (ai.Target != null) ai.ChangeState(EnemyStates.Chase);
        }

        public void Exit(EnemyAI ai) { }
    }

    // ─── Chase ─────────────────────────────────────────────────────────────────

    internal sealed class EnemyChaseState : IEnemyState
    {
        public void Enter(EnemyAI ai) { }

        public void Update(EnemyAI ai)
        {
            if (ai.Target == null) { ai.ChangeState(EnemyStates.Idle); return; }

            float dist = Vector3.Distance(ai.transform.position, ai.Target.position);
            if (ai.Base.Data != null && dist <= ai.Base.Data.attackRange)
            {
                ai.ChangeState(new EnemyAttackState());
                return;
            }

            if (ai.Agent.isOnNavMesh)
                ai.Agent.SetDestination(ai.Target.position);
        }

        public void Exit(EnemyAI ai) { }
    }

    // ─── Attack ────────────────────────────────────────────────────────────────

    internal sealed class EnemyAttackState : IEnemyState
    {
        private float cooldown;

        public void Enter(EnemyAI ai)
        {
            cooldown = 0f;
            if (ai.Agent.isOnNavMesh) ai.Agent.ResetPath();
        }

        public void Update(EnemyAI ai)
        {
            if (ai.Target == null) { ai.ChangeState(EnemyStates.Idle); return; }

            float dist = Vector3.Distance(ai.transform.position, ai.Target.position);
            if (ai.Base.Data == null || dist > ai.Base.Data.attackRange)
            {
                ai.ChangeState(EnemyStates.Chase);
                return;
            }

            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;

            ai.TriggerAttackAnim();

            if (ai.Target.TryGetComponent<PlayerNetworkStats>(out var stats))
                stats.TakeDamage(ai.Base.ScaledAttackPower);
            cooldown = ai.Base.Data.attackInterval;
        }

        public void Exit(EnemyAI ai) { }
    }
}
