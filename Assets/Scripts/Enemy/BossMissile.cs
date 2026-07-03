using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Network;
using Vamsurlike.Player;
using Vamsurlike.VFX;

namespace Vamsurlike.Enemy
{
    // 보스 전용 미사일. 서버만 이동·충돌을 처리한다.
    // Linear(SpreadShot) 와 Mortar(포물선 낙하) 두 가지 모드 지원.
    [RequireComponent(typeof(NetworkObject))]
    public sealed class BossMissile : NetworkBehaviour
    {
        private const float ArcHeight   = 9f;
        private static readonly Quaternion ModelOffset = Quaternion.Euler(0f, 90f, 0f);

        [SerializeField] private float hitRadius = 1.5f;
        [SerializeField] private VFXSpawnEventSO vfxSpawnEvent;
        [SerializeField] private float impactVFXDuration = 0.3f;

        private GameObject sourcePrefab;
        private bool       wasPoolSpawned;

        private enum Mode { Linear, Mortar }
        private Mode    mode;

        // Linear 공통
        private float   damage;
        private bool    active;

        // Linear 전용
        private Vector3 moveDirection;
        private float   speed;
        private float   remainingLife;

        // Mortar 전용
        private Vector3 startPos;
        private Vector3 targetPos;
        private float   flightTime;
        private float   elapsed;
        private float   impactRadius;

        // 서버 전용 팩토리 — BossPatternController가 호출. PoolManager를 거쳐 스폰하고,
        // 반환된 BossMissile에 InitLinear/InitMortar로 이어서 초기화한다.
        public static BossMissile SpawnAt(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return null;

            bool usingPool = PoolManager.Instance != null;
            NetworkObject obj = PoolManager.GetOrInstantiateNetworkObject(prefab, position, rotation, nameof(BossMissile));
            if (obj == null) return null;

            if (!obj.TryGetComponent<BossMissile>(out var missile))
            {
                Debug.LogWarning($"[{nameof(BossMissile)}] prefab에 BossMissile 컴포넌트가 없습니다. prefab={prefab.name}");
                return null;
            }

            missile.sourcePrefab   = prefab;
            missile.wasPoolSpawned = usingPool;
            obj.Spawn(true);
            return missile;
        }

        // BossPatternController에서 Spawn() 직후 호출 — SpreadShot
        public void InitLinear(Vector3 direction, float missileSpeed, float missileDamage, float lifetime)
        {
            mode          = Mode.Linear;
            moveDirection = direction.normalized;
            speed         = missileSpeed;
            damage        = missileDamage;
            remainingLife = lifetime;
            active        = true;

            transform.rotation = Quaternion.LookRotation(direction.normalized) * ModelOffset;
        }

        // BossPatternController에서 Spawn() 직후 호출 — Mortar
        // flightTime 동안 포물선으로 target에 도달 후 blastRadius 범위 AOE 피해
        public void InitMortar(Vector3 start, Vector3 target, float flight, float missileDamage, float blastRadius)
        {
            mode         = Mode.Mortar;
            startPos     = start;
            targetPos    = target;
            flightTime   = Mathf.Max(0.1f, flight);
            elapsed      = 0f;
            damage       = missileDamage;
            impactRadius = blastRadius;
            active       = true;

            transform.position = start;
            transform.rotation = Quaternion.LookRotation(Vector3.up) * ModelOffset;
        }

        private void Update()
        {
            if (!IsServer || !active) return;

            if (mode == Mode.Linear)
                UpdateLinear();
            else
                UpdateMortar();
        }

        private void UpdateLinear()
        {
            remainingLife -= Time.deltaTime;
            if (remainingLife <= 0f) { DespawnSelf(); return; }

            transform.position += moveDirection * speed * Time.deltaTime;
            CheckHitLinear();
        }

        private void UpdateMortar()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightTime);

            // 포물선 위치: XZ는 선형 보간, Y는 sin 아치
            Vector3 pos = Vector3.Lerp(startPos, targetPos, t);
            pos.y += ArcHeight * Mathf.Sin(t * Mathf.PI);
            transform.position = pos;

            // 진행 방향으로 회전
            float nextT  = Mathf.Clamp01(t + Time.deltaTime / flightTime);
            Vector3 next = Vector3.Lerp(startPos, targetPos, nextT);
            next.y += ArcHeight * Mathf.Sin(nextT * Mathf.PI);
            Vector3 dir  = next - pos;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized) * ModelOffset;

            if (t >= 1f)
            {
                PlayImpactVFX(targetPos);
                DamageInRadius(targetPos, impactRadius);
                DespawnSelf();
            }
        }

        private void CheckHitLinear()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            float sqrR = hitRadius * hitRadius;
            foreach (var client in nm.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                if ((client.PlayerObject.transform.position - transform.position).sqrMagnitude > sqrR) continue;
                if (client.PlayerObject.TryGetComponent(out PlayerNetworkStats stats))
                    stats.TakeDamage(damage);
                PlayImpactVFX(transform.position);
                DespawnSelf();
                return;
            }
        }

        private void DamageInRadius(Vector3 center, float radius)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            float sqrR = radius * radius;
            foreach (var client in nm.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                if ((client.PlayerObject.transform.position - center).sqrMagnitude > sqrR) continue;
                if (client.PlayerObject.TryGetComponent(out PlayerNetworkStats stats))
                    stats.TakeDamage(damage);
            }
        }

        private void PlayImpactVFX(Vector3 position)
        {
            if (!IsServer) return;
            PlayImpactVFXClientRpc(position);
        }

        [ClientRpc]
        private void PlayImpactVFXClientRpc(Vector3 position)
        {
            vfxSpawnEvent?.Raise(new VFXCue(VFXCueIds.BossImpact, position, Vector3.up, 1f, impactVFXDuration, Color.white));
        }

        private void DespawnSelf()
        {
            active = false;
            if (!IsSpawned) return;
            NetworkObject.Despawn(!wasPoolSpawned);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            active = false;
            if (!IsServer || !wasPoolSpawned || sourcePrefab == null) return;
            if (PoolManager.Instance != null)
                PoolManager.Instance.ReturnNetworkObject(sourcePrefab, NetworkObject);
        }
    }
}
