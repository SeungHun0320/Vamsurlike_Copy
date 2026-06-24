using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Vamsurlike.Player;
using Vamsurlike.Stage;

namespace Vamsurlike.Enemy
{
    // 서버 전용 보스 패턴 실행기. 일반 추적/공격 사이에 AI 제어권을 잠시 가져온다.
    public sealed class BossPatternController : MonoBehaviour
    {
        private static readonly int SpeedHash  = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int TauntHash  = Animator.StringToHash("Taunt");

        private const float InitialDelay       = 3f;
        private const float PatternInterval    = 4f;
        private const float SlamTakeoffDelay   = 0.2f;
        private const float SlamJumpDuration   = 0.8f;
        private const float SlamJumpHeight     = 5f;
        private const float SlamRecovery       = 0.5f;
        private const float SlamRadius         = 7f;
        private const float SlamDamageScale    = 1.5f;
        private const float ChargeAimDuration  = 0.5f;
        private const float ChargeDuration     = 1.4f;
        private const float ChargeSpeedScale   = 6f;
        private const float ChargeAcceleration = 120f;
        private const float ChargeAngularSpeed = 1440f;
        private const float ChargeImpactRadius = 4f;
        private const float ChargeDamageScale  = 2f;

        private EnemyNetworkBase enemyBase;
        private EnemyAI          enemyAI;
        private NavMeshAgent      agent;
        private Animator          animator;
        private Coroutine         patternRoutine;
        private float             normalSpeed;
        private float             normalAcceleration;
        private float             normalAngularSpeed;
        private bool              normalUpdatePosition;
        private bool              normalUpdateRotation;

        public void Configure(EnemyNetworkBase owner, EnemyAI ai)
        {
            StopPatterns();

            if (!IsServerActive() || owner == null || ai == null || owner.Data == null || !owner.Data.isBoss)
            {
                enabled = false;
                return;
            }

            enemyBase  = owner;
            enemyAI    = ai;
            agent      = ai.Agent;
            animator   = ai.Anim;
            normalSpeed = agent != null ? agent.speed : owner.Data.moveSpeed;
            normalAcceleration = agent != null ? agent.acceleration : 0f;
            normalAngularSpeed = agent != null ? agent.angularSpeed : 0f;
            normalUpdatePosition = agent == null || agent.updatePosition;
            normalUpdateRotation = agent == null || agent.updateRotation;

            enabled = true;
            patternRoutine = StartCoroutine(RunPatterns());
        }

        public void StopPatterns()
        {
            if (patternRoutine != null)
            {
                StopCoroutine(patternRoutine);
                patternRoutine = null;
            }

            RestoreNormalAI();
            enemyBase = null;
            enemyAI   = null;
            agent     = null;
            animator  = null;
        }

        private IEnumerator RunPatterns()
        {
            yield return new WaitForSeconds(InitialDelay);

            bool useSlam = true;
            while (CanRunPattern())
            {
                if (useSlam)
                    yield return ExecuteSlam();
                else
                    yield return ExecuteCharge();

                useSlam = !useSlam;
                RestoreNormalAI();
                yield return new WaitForSeconds(PatternInterval);
            }

            RestoreNormalAI();
            patternRoutine = null;
        }

        // 패턴 1: 플레이어 위치로 포물선 점프 후 착지 순간 광역 피해.
        private IEnumerator ExecuteSlam()
        {
            Transform target = FindClosestAlivePlayer();
            if (target == null) yield break;

            SuspendNormalAI();
            animator?.SetTrigger(TauntHash);

            Vector3 startPosition = transform.position;
            Vector3 landingPosition = target.position;
            if (NavMesh.SamplePosition(landingPosition, out NavMeshHit landingHit, 4f, NavMesh.AllAreas))
                landingPosition = landingHit.position;

            Vector3 facing = landingPosition - startPosition;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);

            enemyBase.ShowSlamTelegraph(
                landingPosition,
                SlamRadius,
                SlamTakeoffDelay + SlamJumpDuration);

            yield return new WaitForSeconds(SlamTakeoffDelay);

            if (agent != null && agent.enabled)
            {
                agent.updatePosition = false;
                agent.updateRotation = false;
            }

            float elapsed = 0f;
            while (elapsed < SlamJumpDuration && CanRunPattern())
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / SlamJumpDuration);
                Vector3 position = Vector3.Lerp(startPosition, landingPosition, progress);
                position.y += Mathf.Sin(progress * Mathf.PI) * SlamJumpHeight;
                transform.position = position;
                yield return null;
            }

            bool landedNormally = elapsed >= SlamJumpDuration && CanRunPattern();
            FinishManualMovement(landedNormally ? landingPosition : transform.position);

            if (landedNormally)
                DamagePlayersInRadius(landingPosition, SlamRadius, enemyBase.ScaledAttackPower * SlamDamageScale);

            yield return new WaitForSeconds(SlamRecovery);
        }

        // 패턴 2: 짧게 플레이어를 조준한 뒤 마지막 방향을 잠그고 직선 돌진.
        private IEnumerator ExecuteCharge()
        {
            Transform target = FindClosestAlivePlayer();
            if (target == null) yield break;

            SuspendNormalAI();
            Vector3 lockedDirection = Vector3.zero;
            float aimElapsed = 0f;
            while (aimElapsed < ChargeAimDuration && CanRunPattern() && target != null)
            {
                Vector3 direction = target.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    lockedDirection = direction.normalized;
                    Quaternion targetRotation = Quaternion.LookRotation(lockedDirection, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRotation,
                        ChargeAngularSpeed * Time.deltaTime);
                }

                aimElapsed += Time.deltaTime;
                yield return null;
            }

            if (lockedDirection.sqrMagnitude <= 0f ||
                agent == null || !agent.enabled || !agent.isOnNavMesh)
                yield break;

            // 이 시점부터 target 위치는 더 이상 읽지 않는다.
            agent.isStopped   = false;
            agent.speed       = Mathf.Max(normalSpeed, 0.1f) * ChargeSpeedScale;
            agent.acceleration = Mathf.Max(normalAcceleration, ChargeAcceleration);
            agent.angularSpeed = Mathf.Max(normalAngularSpeed, ChargeAngularSpeed);
            transform.rotation = Quaternion.LookRotation(lockedDirection, Vector3.up);

            float elapsed = 0f;
            while (elapsed < ChargeDuration && CanRunPattern())
            {
                agent.Move(lockedDirection * agent.speed * Time.deltaTime);

                animator?.SetFloat(SpeedHash, agent.speed);
                elapsed += Time.deltaTime;
                yield return null;
            }

            StopAgent();
            animator?.SetTrigger(AttackHash);
            DamagePlayersInRadius(transform.position, ChargeImpactRadius, enemyBase.ScaledAttackPower * ChargeDamageScale);
            yield return new WaitForSeconds(SlamRecovery);
        }

        private void SuspendNormalAI()
        {
            if (enemyAI != null) enemyAI.enabled = false;
            StopAgent();
        }

        private void RestoreNormalAI()
        {
            if (agent != null && agent.enabled)
            {
                if (agent.updatePosition != normalUpdatePosition || agent.updateRotation != normalUpdateRotation)
                    FinishManualMovement(transform.position);

                agent.speed = normalSpeed;
                agent.acceleration = normalAcceleration;
                agent.angularSpeed = normalAngularSpeed;
                if (agent.isOnNavMesh) agent.isStopped = false;
            }

            animator?.SetFloat(SpeedHash, 0f);
            if (enemyAI != null && enemyBase != null && enemyBase.IsAlive)
                enemyAI.enabled = true;
        }

        private void FinishManualMovement(Vector3 desiredPosition)
        {
            if (agent == null || !agent.enabled) return;

            Vector3 finalPosition = desiredPosition;
            if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                finalPosition = hit.position;

            agent.Warp(finalPosition);
            agent.nextPosition  = finalPosition;
            transform.position  = finalPosition;
            agent.updatePosition = normalUpdatePosition;
            agent.updateRotation = normalUpdateRotation;
        }

        private void StopAgent()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            animator?.SetFloat(SpeedHash, 0f);
        }

        private bool CanRunPattern()
        {
            return IsServerActive()
                && enemyBase != null
                && enemyBase.IsAlive
                && GameFlowCoordinator.Instance != null
                && GameFlowCoordinator.Instance.IsGameplayActive
                && GameFlowCoordinator.Instance.IsBossPhase;
        }

        private Transform FindClosestAlivePlayer()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null) return null;

            Transform closest = null;
            float closestSqrDistance = float.MaxValue;
            Vector3 origin = transform.position;

            foreach (NetworkClient client in manager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                if (!client.PlayerObject.TryGetComponent(out PlayerNetworkStats stats) || !stats.IsAlive) continue;

                float sqrDistance = (client.PlayerObject.transform.position - origin).sqrMagnitude;
                if (sqrDistance >= closestSqrDistance) continue;

                closestSqrDistance = sqrDistance;
                closest = client.PlayerObject.transform;
            }

            return closest;
        }

        private static void DamagePlayersInRadius(Vector3 center, float radius, float damage)
        {
            if (!IsServerActive() || damage <= 0f) return;

            NetworkManager manager = NetworkManager.Singleton;
            float sqrRadius = radius * radius;
            foreach (NetworkClient client in manager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                if ((client.PlayerObject.transform.position - center).sqrMagnitude > sqrRadius) continue;
                if (client.PlayerObject.TryGetComponent(out PlayerNetworkStats stats))
                    stats.TakeDamage(damage);
            }
        }

        private static bool IsServerActive() =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void OnDisable()
        {
            if (patternRoutine != null)
            {
                StopCoroutine(patternRoutine);
                patternRoutine = null;
            }
            RestoreNormalAI();
        }
    }
}
