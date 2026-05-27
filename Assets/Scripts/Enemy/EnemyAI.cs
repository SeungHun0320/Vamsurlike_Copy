using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Vamsurlike.Data;
using Vamsurlike.Player;

namespace Vamsurlike.Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyNetworkBase))]
    public class EnemyAI : NetworkBehaviour
    {
        internal NavMeshAgent Agent      { get; private set; }
        internal EnemyNetworkBase Base   { get; private set; }
        internal Transform Target        { get; private set; }

        private IEnemyState currentState;
        private float targetUpdateTimer;
        private const float TargetUpdateInterval = 0.5f;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Base  = GetComponent<EnemyNetworkBase>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) { Agent.enabled = false; enabled = false; return; }
            // Data는 EnemyNetworkBase.Initialize 후 ApplyData로 주입됨 — 여기서 읽지 않음
            ChangeState(new EnemyIdleState());
        }

        // EnemyNetworkBase.Initialize 직후 서버에서 호출
        internal void ApplyData(EnemyDataSO data)
        {
            if (data == null) return;
            Agent.speed            = data.moveSpeed;
            Agent.stoppingDistance = Mathf.Max(0.1f, data.attackRange * 0.8f);
        }

        private void Update()
        {
            if (!IsServer || !Base.IsAlive) return;

            targetUpdateTimer -= Time.deltaTime;
            if (targetUpdateTimer <= 0f)
            {
                targetUpdateTimer = TargetUpdateInterval;
                RefreshTarget();
            }

            currentState?.Update(this);
        }

        internal void ChangeState(IEnemyState next)
        {
            currentState?.Exit(this);
            currentState = next;
            currentState.Enter(this);
        }

        private void RefreshTarget()
        {
            float     closest  = float.MaxValue;
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

    // ─── Idle ──────────────────────────────────────────────────────────────────

    internal sealed class EnemyIdleState : IEnemyState
    {
        public void Enter(EnemyAI ai)
        {
            if (ai.Agent.isOnNavMesh) ai.Agent.ResetPath();
        }

        public void Update(EnemyAI ai)
        {
            if (ai.Target != null) ai.ChangeState(new EnemyChaseState());
        }

        public void Exit(EnemyAI ai) { }
    }

    // ─── Chase ─────────────────────────────────────────────────────────────────

    internal sealed class EnemyChaseState : IEnemyState
    {
        public void Enter(EnemyAI ai) { }

        public void Update(EnemyAI ai)
        {
            if (ai.Target == null) { ai.ChangeState(new EnemyIdleState()); return; }

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
            if (ai.Target == null) { ai.ChangeState(new EnemyIdleState()); return; }

            float dist = Vector3.Distance(ai.transform.position, ai.Target.position);
            if (ai.Base.Data == null || dist > ai.Base.Data.attackRange)
            {
                ai.ChangeState(new EnemyChaseState());
                return;
            }

            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;

            if (ai.Target.TryGetComponent<PlayerNetworkStats>(out var stats))
                stats.TakeDamage(ai.Base.Data.attackPower);
            cooldown = ai.Base.Data.attackInterval;
        }

        public void Exit(EnemyAI ai) { }
    }
}
