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
        private static readonly int SpeedHash   = Animator.StringToHash("Speed");
        private static readonly int AttackHash  = Animator.StringToHash("Attack");
        private static readonly int TauntHash   = Animator.StringToHash("Taunt");
        private static readonly int BigShotHash = Animator.StringToHash("BigShot");

        private const float InitialDelay       = 3f;
        private const float PatternInterval    = 4f;

        // Slam
        private const float SlamTakeoffDelay   = 0.2f;
        private const float SlamJumpDuration   = 0.8f;
        private const float SlamJumpHeight     = 5f;
        private const float SlamRecovery       = 0.5f;
        private const float SlamRadius         = 7f;
        private const float SlamDamageScale    = 1.5f;

        // Charge
        private const float ChargeAimDuration  = 0.5f;
        private const float ChargeDuration     = 1.4f;
        private const float ChargeSpeedScale   = 6f;
        private const float ChargeAcceleration = 120f;
        private const float ChargeAngularSpeed = 1440f;
        private const float ChargeImpactRadius = 4f;
        private const float ChargeDamageScale  = 2f;

        // Mortar (박격포)
        private const int   MortarCount             = 6;
        private const float MortarSpread            = 12f;
        private const float MortarAimDuration       = 0.6f;
        private const float MortarTelegraphDuration = 1.8f;
        private const float MortarImpactRadius      = 3.5f;
        private const float MortarDamageScale       = 0.8f;
        private const float MortarRecovery          = 0.5f;

        // SpreadShot (산탄 미사일)
        private const int   SpreadCount        = 5;
        private const float SpreadHalfAngle    = 40f;
        private const float SpreadAimDuration  = 0.4f;
        private const float SpreadMissileSpeed = 18f;
        private const float SpreadMissileLife  = 2.5f;
        private const float SpreadDamageScale  = 1.0f;
        private const float SpreadInterval     = 0.12f;
        private const float SpreadRecovery     = 0.6f;

        // CircleBurst (360° 동시 발사 반복)
        private const int   RingCount               = 12;   // 한 번에 발사하는 수
        private const int   RingRepeats             = 2;    // 반복 횟수
        private const float RingMissileSpeed        = 14f;
        private const float RingMissileLife         = 3.0f;
        private const float RingDamageScale         = 0.6f;
        private const float RingRoundPause          = 0.4f; // 라운드 사이 대기
        private const float RingAimDuration         = 0.3f;
        private const float RingRecovery            = 0.5f;
        private const float RingAngleOffsetPerRound = 360f / RingCount / RingRepeats;

        // EnemyDataSO.bossMissilePrefab에서 Configure() 시점에 주입됨
        private GameObject missilePrefab;

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

            enemyBase     = owner;
            enemyAI       = ai;
            agent         = ai.Agent;
            animator      = ai.Anim;
            missilePrefab = owner.Data.bossMissilePrefab;
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

            // 순서: Slam → Mortar → Charge → SpreadShot → 반복
            int index = 0;
            while (CanRunPattern())
            {
                yield return index switch
                {
                    0 => ExecuteSlam(),
                    1 => ExecuteMortar(),
                    2 => ExecuteCharge(),
                    3 => ExecuteSpreadShot(),
                    _ => ExecuteCircleBurst(),
                };

                index = (index + 1) % 5;
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

        // 패턴 3: 여러 위치에 경고원을 띄우고, 동시에 포물선 미사일을 발사.
        // 미사일이 flightTime(= MortarTelegraphDuration) 동안 날아 착탄 시 AOE 피해.
        private IEnumerator ExecuteMortar()
        {
            SuspendNormalAI();
            animator?.SetTrigger(BigShotHash);

            yield return new WaitForSeconds(MortarAimDuration);
            if (!CanRunPattern()) yield break;

            Vector3   center  = GetPlayerClusterCenter();
            Vector3[] targets = PickMortarTargets(center, MortarCount, MortarSpread);

            enemyBase.ShowMortarTelegraph(targets, MortarImpactRadius, MortarTelegraphDuration);

            // 경고원과 동시에 발사 — 비행 시간이 경고 지속 시간과 같으므로 경고 끝날 때 착탄
            Vector3 firePos = transform.position + Vector3.up * 1.5f;
            float   dmg     = enemyBase.ScaledAttackPower * MortarDamageScale;
            foreach (var target in targets)
                SpawnMortarMissile(firePos, target, MortarTelegraphDuration, dmg, MortarImpactRadius);

            yield return new WaitForSeconds(MortarTelegraphDuration + MortarRecovery);
        }

        // 패턴 4: 플레이어 방향 기준 ±SpreadHalfAngle 범위 무작위 산탄.
        private IEnumerator ExecuteSpreadShot()
        {
            Transform target = FindClosestAlivePlayer();
            if (target == null) yield break;

            SuspendNormalAI();

            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);

            if (animator != null) animator.SetTrigger(BigShotHash);
            yield return new WaitForSeconds(SpreadAimDuration);
            if (!CanRunPattern()) yield break;

            Vector3 baseDir  = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : transform.forward;
            Vector3 spawnPos = transform.position + Vector3.up * 1.2f;

            for (int i = 0; i < SpreadCount; i++)
            {
                if (!CanRunPattern()) break;

                float   angle = Random.Range(-SpreadHalfAngle, SpreadHalfAngle);
                Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * baseDir;
                SpawnMissile(spawnPos, dir);

                yield return new WaitForSeconds(SpreadInterval);
            }

            yield return new WaitForSeconds(SpreadRecovery);
        }

        // 패턴 5: 360° 동시 발사를 반복하되, 매 라운드 시작 각도만 변경.
        private IEnumerator ExecuteCircleBurst()
        {
            SuspendNormalAI();
            if (animator != null) animator.SetTrigger(BigShotHash);
            yield return new WaitForSeconds(RingAimDuration);
            if (!CanRunPattern()) yield break;

            Vector3 spawnPos = transform.position + Vector3.up * 1.2f;
            float   stepAngle = 360f / RingCount;
            float   dmg       = enemyBase.ScaledAttackPower * RingDamageScale;

            for (int round = 0; round < RingRepeats; round++)
            {
                if (!CanRunPattern()) yield break;

                float offset = round * RingAngleOffsetPerRound;
                spawnPos = transform.position + Vector3.up * 1.2f;

                for (int i = 0; i < RingCount; i++)
                {
                    float   angle = offset + stepAngle * i;
                    Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    SpawnRingMissile(spawnPos, dir, dmg);
                }

                if (round < RingRepeats - 1)
                    yield return new WaitForSeconds(RingRoundPause);
            }

            yield return new WaitForSeconds(RingRecovery);
        }

        private void SpawnRingMissile(Vector3 spawnPos, Vector3 direction, float damage)
        {
            if (missilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(BossPatternController)}] missilePrefab이 설정되지 않았습니다.");
                return;
            }

            var go = Instantiate(missilePrefab, spawnPos, Quaternion.identity);
            if (!go.TryGetComponent(out NetworkObject net))
            {
                Destroy(go);
                return;
            }

            net.Spawn(true);
            if (net.TryGetComponent(out BossMissile missile))
                missile.InitLinear(direction, RingMissileSpeed, damage, RingMissileLife);
        }

        private void SpawnMissile(Vector3 position, Vector3 direction)
        {
            if (missilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(BossPatternController)}] missilePrefab이 설정되지 않았습니다.");
                return;
            }

            var go = Instantiate(missilePrefab, position, Quaternion.LookRotation(direction));
            if (!go.TryGetComponent(out NetworkObject net)) { Destroy(go); return; }

            net.Spawn(true);
            if (net.TryGetComponent(out BossMissile missile))
                missile.InitLinear(direction, SpreadMissileSpeed,
                    enemyBase.ScaledAttackPower * SpreadDamageScale, SpreadMissileLife);
        }

        private void SpawnMortarMissile(Vector3 firePos, Vector3 target, float flightTime, float dmg, float blastRadius)
        {
            if (missilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(BossPatternController)}] missilePrefab이 설정되지 않았습니다.");
                return;
            }

            var go = Instantiate(missilePrefab, firePos, Quaternion.identity);
            if (!go.TryGetComponent(out NetworkObject net)) { Destroy(go); return; }

            net.Spawn(true);
            if (net.TryGetComponent(out BossMissile missile))
                missile.InitMortar(firePos, target, flightTime, dmg, blastRadius);
        }

        private Vector3 GetPlayerClusterCenter()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return transform.position;

            Vector3 sum = Vector3.zero;
            int     cnt = 0;
            foreach (var client in nm.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                if (!client.PlayerObject.TryGetComponent(out PlayerNetworkStats stats) || !stats.IsAlive) continue;
                sum += client.PlayerObject.transform.position;
                cnt++;
            }
            return cnt > 0 ? sum / cnt : transform.position;
        }

        private static Vector3[] PickMortarTargets(Vector3 center, int count, float spread)
        {
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * spread;
                Vector3 pos    = center + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    positions[i] = hit.position;
                else
                    positions[i] = pos;
            }
            return positions;
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
