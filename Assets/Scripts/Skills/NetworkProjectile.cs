using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Network;
using Vamsurlike.Stage;

namespace Vamsurlike.Skills
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkProjectile : NetworkBehaviour
    {
        [SerializeField] private Vector3 visualRotationOffsetEuler;

        private const float DefaultHomingSearchRange = 18f;
        private const float DefaultHomingTurnDegreesPerSecond = 540f;

        private readonly List<Renderer> projectileRenderers = new();

        private GameObject sourcePrefab;
        private SkillLevelData currentLevelData;
        private NormalProjectileMode normalMode;
        private OrbitingProjectileMode orbitingMode;
        private ProjectileModeBase activeMode;
        private ProjectileOrbitGroup orbitGroup;
        private bool hasInitialized;

        internal ulong ProjectileOwnerClientId { get; private set; }
        internal string SkillTag { get; private set; }
        internal Vector3 Direction { get; set; }
        internal float Speed { get; private set; }
        internal float Damage { get; set; }
        internal float HitRadius { get; private set; }
        internal float RemainingLifetime { get; set; }
        internal int PierceRemaining { get; private set; }
        internal float SpeedMultiplier { get; private set; } = 1f;
        internal SkillLevelData CurrentLevelData => currentLevelData;
        internal bool HasInitialized => hasInitialized;

        internal float OrbitHitCooldown => currentLevelData != null
            ? Mathf.Max(0.01f, currentLevelData.orbitalHitCooldown)
            : 0.2f;

        internal float OrbitalProjectileScale => currentLevelData != null
            ? Mathf.Max(0.01f, currentLevelData.orbitalProjectileScale)
            : 0.75f;

        internal float DetachedOrbitalLifetime => currentLevelData != null
            ? Mathf.Max(0.25f, currentLevelData.projectileLifetime * Mathf.Max(0.1f, currentLevelData.detachedOrbitalLifetimeMultiplier))
            : 0.25f;

        internal float DetachedOrbitalHomingDelay => currentLevelData != null
            ? Mathf.Max(0f, currentLevelData.detachedOrbitalHomingDelay)
            : 0.2f;

        internal float DetachedOrbitalHomingRange => currentLevelData != null
            ? Mathf.Max(HitRadius, currentLevelData.detachedOrbitalHomingRange)
            : Mathf.Max(HitRadius, DefaultHomingSearchRange);

        internal float HomingTurnDegreesPerSecond => currentLevelData != null
            ? Mathf.Max(1f, currentLevelData.detachedOrbitalHomingTurnSpeed)
            : DefaultHomingTurnDegreesPerSecond;

        private void Awake()
        {
            EnsureModesInitialized();
        }

        private void EnsureModesInitialized()
        {
            normalMode ??= new NormalProjectileMode(this);
            orbitingMode ??= new OrbitingProjectileMode(this);
            orbitGroup ??= new ProjectileOrbitGroup(this);
        }

        public void Initialize(
            GameObject projectilePrefab,
            ulong owner,
            Vector3 spawnPosition,
            Vector3 fireDirection,
            SkillLevelData levelData,
            float finalDamage = -1f,
            float speedMultiplier = 1f,
            string skillTag = null)
        {
            // NetworkObject.Spawn() 이전(아직 스폰되지 않은 상태)에 호출되므로 this.IsServer(스폰 상태에
            // 의존) 대신 NetworkManager.Singleton을 직접 확인한다.
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (levelData == null) return;

            EnsureModesInitialized();

            sourcePrefab = projectilePrefab;
            ProjectileOwnerClientId = owner;
            SkillTag = skillTag;
            currentLevelData = levelData;
            SpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
            Direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : Vector3.forward;
            Speed = levelData.projectileSpeed * SpeedMultiplier;
            Damage = finalDamage >= 0f ? finalDamage : levelData.damage;
            HitRadius = levelData.projectileHitRadius;
            RemainingLifetime = levelData.projectileLifetime;
            PierceRemaining = Mathf.Max(0, levelData.pierceCount);
            hasInitialized = true;

            transform.localScale = projectilePrefab != null ? projectilePrefab.transform.localScale : Vector3.one;
            transform.position = spawnPosition;
            transform.rotation = GetProjectileRotation(Direction);
            SetProjectileRenderersVisible(true);

            SetMode(normalMode);
        }

        public Quaternion GetProjectileRotation(Vector3 forward)
        {
            Vector3 safeForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            return Quaternion.LookRotation(safeForward, Vector3.up) * Quaternion.Euler(visualRotationOffsetEuler);
        }

        public void EnableHoming(float searchRange = DefaultHomingSearchRange, float delay = 0f)
        {
            SwitchToNormalMode();
            normalMode.EnableHoming(searchRange, delay);
        }

        public void SpawnOrbitingProjectiles(
            GameObject projectilePrefab,
            int count,
            float radius,
            float rotSpeed,
            float orbitalHitRadius,
            float orbitalDamage)
        {
            if (!IsServer || orbitGroup == null) return;

            orbitGroup.Spawn(projectilePrefab, count, radius, rotSpeed, orbitalHitRadius, orbitalDamage);
        }

        internal void ConfigureAsOrbiting(
            NetworkProjectile center,
            float startAngle,
            float radius,
            float rotSpeed,
            float orbitalHitRadius,
            float orbitalDamage)
        {
            EnsureModesInitialized();
            orbitingMode.Configure(center, startAngle, radius, rotSpeed, orbitalHitRadius, orbitalDamage);
            SetMode(orbitingMode);
        }

        internal void DetachFromOrbit()
        {
            orbitingMode.DetachFromOrbit();
        }

        internal void SwitchToNormalMode()
        {
            if (activeMode == normalMode) return;

            SetMode(normalMode);
        }

        internal bool TryHitEnemy(HashSet<ulong> hitEnemyIds)
        {
            EnemyNetworkBase target = AutoTargeting.FindNearestEnemy(transform.position, HitRadius, hitEnemyIds);
            if (target == null) return false;

            hitEnemyIds.Add(target.NetworkObjectId);
            target.TakeDamage(Damage, ProjectileOwnerClientId, SkillTag);
            LifestealUtility.Apply(currentLevelData, ProjectileOwnerClientId, Damage);

            if (PierceRemaining <= 0)
                return true;

            PierceRemaining--;
            return false;
        }

        internal void DespawnToPool()
        {
            orbitGroup?.DetachAll();
            activeMode?.Exit();

            hasInitialized = false;
            transform.localScale = sourcePrefab != null ? sourcePrefab.transform.localScale : Vector3.one;
            normalMode?.Reset();

            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(false);
        }

        // 씬 리로드(재시작) 직후처럼 로딩/GC로 어느 한 프레임의 Time.deltaTime이 비정상적으로 커지면,
        // 그 프레임 하나에서 이동/궤도 회전이 과도하게 진행되어 "한순간 엄청 빠르게 움직이다 정상
        // 속도로 돌아오는" 것처럼 보인다 — 한 프레임이 진행할 수 있는 시간 폭을 제한해 방지한다.
        private const float MaxDeltaTime = 0.1f;

        private void Update()
        {
            if (!IsServer || !hasInitialized) return;
            if (GameFlowCoordinator.Instance == null || !GameFlowCoordinator.Instance.IsGameplayActive) return;

            activeMode?.Tick(Mathf.Min(Time.deltaTime, MaxDeltaTime));
        }

        public override void OnNetworkDespawn()
        {
            SetProjectileRenderersVisible(true);
            orbitGroup?.DetachAll();

            if (IsServer && sourcePrefab != null && PoolManager.Instance != null)
                PoolManager.Instance.ReturnNetworkObject(sourcePrefab, NetworkObject);

            base.OnNetworkDespawn();
        }

        private void SetMode(ProjectileModeBase nextMode)
        {
            if (activeMode == nextMode) return;

            activeMode?.Exit();
            activeMode = nextMode;
            activeMode?.Enter();
        }

        private void SetProjectileRenderersVisible(bool visible)
        {
            CacheProjectileRenderers();
            for (int i = 0; i < projectileRenderers.Count; i++)
            {
                if (projectileRenderers[i] != null)
                    projectileRenderers[i].enabled = visible;
            }
        }

        private void CacheProjectileRenderers()
        {
            if (projectileRenderers.Count > 0) return;

            GetComponentsInChildren(true, projectileRenderers);
        }
    }
}
