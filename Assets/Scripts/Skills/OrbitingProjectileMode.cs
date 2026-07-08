using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    internal sealed class OrbitingProjectileMode : ProjectileModeBase
    {
        private readonly HashSet<ulong> frameHitIds = new();
        private readonly Dictionary<ulong, float> hitCooldownUntil = new();

        private NetworkProjectile center;
        private float angle;
        private float radius;
        private float rotSpeed;
        private float hitRadius;
        private Vector3 previousCenterPosition;
        private Vector3 centerVelocity;
        private bool hasPreviousCenterPosition;

        private static readonly Collider[] HitBuffer = new Collider[16];
        private static readonly int EnemyLayer = 1 << 7;

        public OrbitingProjectileMode(NetworkProjectile projectile) : base(projectile) { }

        public void Configure(
            NetworkProjectile orbitCenter,
            float startAngle,
            float orbitRadius,
            float orbitRotSpeed,
            float orbitHitRadius,
            float orbitalDamage)
        {
            center = orbitCenter;
            angle = startAngle;
            radius = Mathf.Max(0.1f, orbitRadius);
            rotSpeed = orbitRotSpeed;
            hitRadius = Mathf.Max(0.05f, orbitHitRadius);
            hasPreviousCenterPosition = false;
            centerVelocity = Vector3.zero;
            frameHitIds.Clear();
            hitCooldownUntil.Clear();

            Projectile.Damage = Mathf.Max(0f, orbitalDamage);
            Projectile.transform.localScale = Vector3.one * Projectile.OrbitalProjectileScale;

            float centerLifetime = center != null ? center.RemainingLifetime : Projectile.RemainingLifetime;
            Projectile.RemainingLifetime = centerLifetime + Projectile.DetachedOrbitalLifetime;
        }

        public override void Tick(float deltaTime)
        {
            if (center == null || !center.HasInitialized || center.NetworkObject == null || !center.NetworkObject.IsSpawned)
            {
                DetachFromOrbit();
                return;
            }

            Vector3 centerPosition = center.transform.position;
            if (hasPreviousCenterPosition && deltaTime > 0.0001f)
                centerVelocity = (centerPosition - previousCenterPosition) / deltaTime;

            previousCenterPosition = centerPosition;
            hasPreviousCenterPosition = true;

            angle = (angle + rotSpeed * deltaTime) % 360f;
            Vector3 radial = ProjectileOrbitMath.GetRadial(angle);
            Vector3 tangent = ProjectileOrbitMath.GetTangent(radial, rotSpeed);
            Vector3 orbitVelocity = GetOrbitVelocity(tangent);

            Projectile.transform.position = centerPosition + radial * radius;
            Projectile.Direction = orbitVelocity.sqrMagnitude > 0.0001f ? orbitVelocity.normalized : tangent;
            Projectile.transform.rotation = Projectile.GetProjectileRotation(Projectile.Direction);

            TickDamage();

            Projectile.RemainingLifetime -= deltaTime;
            if (Projectile.RemainingLifetime <= 0f)
                Projectile.DespawnToPool();
        }

        public void DetachFromOrbit()
        {
            center = null;
            hasPreviousCenterPosition = false;
            frameHitIds.Clear();
            hitCooldownUntil.Clear();

            Projectile.SwitchToNormalMode();
            Projectile.RemainingLifetime = Projectile.DetachedOrbitalLifetime;

            if (Projectile.Direction.sqrMagnitude <= 0.0001f)
                Projectile.Direction = Projectile.transform.forward;

            Projectile.transform.rotation = Projectile.GetProjectileRotation(Projectile.Direction);
            Projectile.EnableHoming(Projectile.DetachedOrbitalHomingRange, Projectile.DetachedOrbitalHomingDelay);
        }

        public override void Exit()
        {
            center = null;
            hasPreviousCenterPosition = false;
            frameHitIds.Clear();
            hitCooldownUntil.Clear();
        }

        private void TickDamage()
        {
            frameHitIds.Clear();

            int hitCount = Physics.OverlapSphereNonAlloc(
                Projectile.transform.position,
                hitRadius,
                HitBuffer,
                EnemyLayer);

            for (int i = 0; i < hitCount; i++)
            {
                if (!HitBuffer[i].TryGetComponent<EnemyNetworkBase>(out var enemy)) continue;
                if (!enemy.IsAlive) continue;
                if (!frameHitIds.Add(enemy.NetworkObjectId)) continue;
                if (hitCooldownUntil.TryGetValue(enemy.NetworkObjectId, out float cooldownUntil)
                    && cooldownUntil > Time.time) continue;

                hitCooldownUntil[enemy.NetworkObjectId] = Time.time + Projectile.OrbitHitCooldown;
                enemy.TakeDamage(Projectile.Damage, Projectile.ProjectileOwnerClientId, Projectile.SkillTag);
                LifestealUtility.Apply(Projectile.CurrentLevelData, Projectile.ProjectileOwnerClientId, Projectile.Damage);
            }
        }

        private Vector3 GetOrbitVelocity(Vector3 tangent)
        {
            float tangentSpeed = Mathf.Abs(rotSpeed) * Mathf.Deg2Rad * Mathf.Max(0f, radius);
            if (tangentSpeed <= 0.01f)
                tangentSpeed = Mathf.Max(0.1f, Projectile.Speed);

            return centerVelocity + tangent.normalized * tangentSpeed;
        }
    }
}
