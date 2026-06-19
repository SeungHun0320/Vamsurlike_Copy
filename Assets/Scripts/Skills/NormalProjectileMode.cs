using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    internal sealed class NormalProjectileMode : ProjectileModeBase
    {
        private readonly HashSet<ulong> hitEnemyIds = new();
        private bool isHoming;
        private float homingDelayRemaining;
        private float homingSearchRange;
        private float homingTurnDegreesPerSecond;
        private EnemyNetworkBase homingTarget;

        public NormalProjectileMode(NetworkProjectile projectile) : base(projectile) { }

        public override void Enter()
        {
            Reset();
        }

        public override void Tick(float deltaTime)
        {
            UpdateHomingDirection(deltaTime);

            Projectile.transform.position += Projectile.Direction * (Projectile.Speed * deltaTime);
            Projectile.RemainingLifetime -= deltaTime;

            if (Projectile.TryHitEnemy(hitEnemyIds) || Projectile.RemainingLifetime <= 0f)
                Projectile.DespawnToPool();
        }

        public void EnableHoming(float searchRange, float delay)
        {
            isHoming = true;
            homingDelayRemaining = Mathf.Max(0f, delay);
            homingSearchRange = Mathf.Max(Projectile.HitRadius, searchRange);
            homingTurnDegreesPerSecond = Projectile.HomingTurnDegreesPerSecond;
            homingTarget = AutoTargeting.FindNearestEnemy(Projectile.transform.position, homingSearchRange, hitEnemyIds);
        }

        public void ClearHits()
        {
            hitEnemyIds.Clear();
        }

        public void Reset()
        {
            isHoming = false;
            homingDelayRemaining = 0f;
            homingTarget = null;
            homingTurnDegreesPerSecond = Projectile.HomingTurnDegreesPerSecond;
            hitEnemyIds.Clear();
        }

        private void UpdateHomingDirection(float deltaTime)
        {
            if (!isHoming) return;
            if (homingDelayRemaining > 0f)
            {
                homingDelayRemaining -= deltaTime;
                return;
            }

            if (homingTarget == null || !homingTarget.IsAlive)
                homingTarget = AutoTargeting.FindNearestEnemy(Projectile.transform.position, homingSearchRange, hitEnemyIds);

            if (homingTarget == null) return;

            Vector3 desired = homingTarget.transform.position - Projectile.transform.position;
            desired.y = 0f;
            if (desired.sqrMagnitude <= 0.0001f) return;

            float maxRadians = homingTurnDegreesPerSecond * Mathf.Deg2Rad * deltaTime;
            Projectile.Direction = Vector3.RotateTowards(
                Projectile.Direction,
                desired.normalized,
                maxRadians,
                0f).normalized;

            Projectile.transform.rotation = Projectile.GetProjectileRotation(Projectile.Direction);
        }
    }
}
