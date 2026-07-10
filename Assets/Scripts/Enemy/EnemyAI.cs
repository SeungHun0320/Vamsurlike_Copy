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
        private float nextRepathTime;
        private Vector3 lastDestination;
        private float animUpdateTimer;
        private float lastAnimSpeed = -1f;

        private const float TargetUpdateInterval = 0.5f;
        private const float RepathInterval = 0.2f;
        private const float RepathTargetMoveThresholdSqr = 0.25f;
        private const float AnimUpdateInterval = 0.1f;
        private const float AnimSpeedChangeThreshold = 0.05f;

        internal float AttackCooldown { get; set; }

        private float stunnedUntil;
        internal bool IsStunned => Time.time < stunnedUntil;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Base  = GetComponent<EnemyNetworkBase>();
            Anim  = GetComponentInChildren<Animator>();

            if (Agent == null)
            {
                Debug.LogError($"[{nameof(EnemyAI)}] NavMeshAgent 컴포넌트를 찾을 수 없습니다.", this);
                enabled = false;
                return;
            }
            if (Base == null)
            {
                Debug.LogError($"[{nameof(EnemyAI)}] EnemyNetworkBase 컴포넌트를 찾을 수 없습니다.", this);
                enabled = false;
                return;
            }

            // 클라이언트에서 NavMeshAgent가 활성화된 채로 남으면 NavMesh 오류 발생.
            // OnServerSpawned()에서 서버에서만 다시 켠다.
            Agent.autoRepath = false;
            Agent.enabled = false;
        }

        protected override void OnServerSpawned()
        {
            Agent.enabled = true;
            targetUpdateTimer = Random.Range(0f, TargetUpdateInterval);
            nextRepathTime = Time.time + Random.Range(0f, RepathInterval);
            lastDestination = transform.position;
            AttackCooldown = 0f;
            animUpdateTimer = Random.Range(0f, AnimUpdateInterval);
            lastAnimSpeed = -1f;
            ChangeState(EnemyStates.Idle);
        }

        internal void ApplyData(EnemyDataSO data)
        {
            if (data == null) return;
            Agent.speed            = data.moveSpeed;
            Agent.stoppingDistance = Mathf.Max(0.1f, data.attackRange * 0.8f);
        }

        // 대지분쇄자 등 CC 스킬 전용 — 중복 적용 시 더 긴 쪽으로 갱신(스택 대신 최댓값 유지).
        internal void ApplyStun(float duration)
        {
            if (duration <= 0f) return;
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
        }

        // 궤도 수류탄 등 넉백 스킬 전용 — NavMeshAgent.Move는 내부적으로 NavMesh 경계에 클램프된다.
        internal void ApplyKnockback(Vector3 direction, float force)
        {
            if (force <= 0f || Agent == null || !Agent.enabled || !Agent.isOnNavMesh) return;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Agent.Move(direction.normalized * force);
        }

        private void Update()
        {
            if (!Base.IsAlive) return;
            if (GameFlowCoordinator.Instance == null || !GameFlowCoordinator.Instance.IsGameplayActive) return;

            if (IsStunned)
            {
                // ResetPath만으로는 기존 관성 때문에 몇 프레임 더 미끄러지듯 이동할 수 있어
                // velocity까지 명시적으로 0으로 만들어 즉시 멈춘다.
                if (Agent.isOnNavMesh)
                {
                    Agent.ResetPath();
                    Agent.velocity = Vector3.zero;
                }
                SetAnimatorSpeed(0f, force: true);
                return;
            }

            targetUpdateTimer -= Time.deltaTime;
            if (targetUpdateTimer <= 0f)
            {
                targetUpdateTimer = TargetUpdateInterval;
                RefreshTarget();
            }

            currentState?.Update(this);

            UpdateAnimatorSpeed();
        }

        internal void TriggerAttackAnim()
        {
            if (Anim != null) Anim.SetTrigger(AttackHash);
        }

        private void UpdateAnimatorSpeed()
        {
            animUpdateTimer -= Time.deltaTime;
            if (animUpdateTimer > 0f) return;

            animUpdateTimer = AnimUpdateInterval;
            SetAnimatorSpeed(Agent.velocity.magnitude);
        }

        private void SetAnimatorSpeed(float speed, bool force = false)
        {
            if (Anim == null) return;
            if (!force && Mathf.Abs(speed - lastAnimSpeed) < AnimSpeedChangeThreshold) return;

            lastAnimSpeed = speed;
            Anim.SetFloat(SpeedHash, speed);
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
            Vector3   selfPosition  = transform.position;

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                var playerObj = client.PlayerObject;
                if (playerObj == null) continue;

                var stats = playerObj.GetComponent<PlayerNetworkStats>();
                if (stats != null && !stats.IsAlive) continue;

                float sqrDist = Vector3.SqrMagnitude(selfPosition - playerObj.transform.position);
                if (sqrDist < closest) { closest = sqrDist; bestTransform = playerObj.transform; }
            }

            Target = bestTransform;
        }

        internal bool IsTargetInAttackRange()
        {
            if (Target == null || Base.Data == null) return false;
            float range = Base.Data.attackRange;
            return Vector3.SqrMagnitude(transform.position - Target.position) <= range * range;
        }

        internal void UpdateDestinationToTarget()
        {
            if (Target == null || !Agent.isOnNavMesh) return;

            Vector3 targetPosition = Target.position;
            if (Time.time < nextRepathTime && Vector3.SqrMagnitude(targetPosition - lastDestination) < RepathTargetMoveThresholdSqr)
                return;

            lastDestination = targetPosition;
            nextRepathTime = Time.time + RepathInterval;
            Agent.SetDestination(targetPosition);
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
        internal static readonly EnemyIdleState   Idle   = new();
        internal static readonly EnemyChaseState  Chase  = new();
        internal static readonly EnemyAttackState Attack = new();
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

            if (ai.IsTargetInAttackRange())
            {
                ai.ChangeState(EnemyStates.Attack);
                return;
            }

            ai.UpdateDestinationToTarget();
        }

        public void Exit(EnemyAI ai) { }
    }

    // ─── Attack ────────────────────────────────────────────────────────────────

    internal sealed class EnemyAttackState : IEnemyState
    {
        public void Enter(EnemyAI ai)
        {
            ai.AttackCooldown = 0f;
            if (ai.Agent.isOnNavMesh) ai.Agent.ResetPath();
        }

        public void Update(EnemyAI ai)
        {
            if (ai.Target == null) { ai.ChangeState(EnemyStates.Idle); return; }

            if (!ai.IsTargetInAttackRange())
            {
                ai.ChangeState(EnemyStates.Chase);
                return;
            }

            ai.AttackCooldown -= Time.deltaTime;
            if (ai.AttackCooldown > 0f) return;

            ai.TriggerAttackAnim();

            if (ai.Target.TryGetComponent<PlayerNetworkStats>(out var stats))
                stats.TakeDamage(ai.Base.ScaledAttackPower);
            ai.AttackCooldown = ai.Base.Data.attackInterval;
        }

        public void Exit(EnemyAI ai) { }
    }
}
