using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Network;

namespace Vamsurlike.Skills
{
    internal sealed class ProjectileOrbitGroup
    {
        private readonly NetworkProjectile center;
        private readonly List<NetworkProjectile> orbitingProjectiles = new();

        public ProjectileOrbitGroup(NetworkProjectile center)
        {
            this.center = center;
        }

        public void Spawn(
            GameObject projectilePrefab,
            int count,
            float radius,
            float rotSpeed,
            float hitRadius,
            float damage)
        {
            if (center == null || !center.IsServer) return;
            if (projectilePrefab == null || center.CurrentLevelData == null) return;
            if (PoolManager.Instance == null) return;

            DetachAll();

            count = Mathf.Max(0, count);
            radius = Mathf.Max(0.1f, radius);
            hitRadius = Mathf.Max(0.05f, hitRadius);

            for (int i = 0; i < count; i++)
            {
                float angle = 360f / count * i;
                Vector3 radial = ProjectileOrbitMath.GetRadial(angle);
                Vector3 tangent = ProjectileOrbitMath.GetTangent(radial, rotSpeed);
                Vector3 spawnPosition = center.transform.position + radial * radius;
                Vector3 initialDirection = GetInitialDirection(tangent, rotSpeed, radius);

                NetworkObject spawnedObject = PoolManager.Instance.GetNetworkObject(
                    projectilePrefab,
                    spawnPosition,
                    center.GetProjectileRotation(initialDirection));

                if (spawnedObject == null) continue;
                if (!spawnedObject.TryGetComponent<NetworkProjectile>(out var projectile)) continue;

                projectile.Initialize(
                    projectilePrefab,
                    center.ProjectileOwnerClientId,
                    spawnPosition,
                    initialDirection,
                    center.CurrentLevelData,
                    damage,
                    center.SpeedMultiplier,
                    center.SkillTag);

                projectile.ConfigureAsOrbiting(center, angle, radius, rotSpeed, hitRadius, damage);

                if (!spawnedObject.IsSpawned)
                    spawnedObject.Spawn(true);

                orbitingProjectiles.Add(projectile);
            }
        }

        public void DetachAll()
        {
            for (int i = orbitingProjectiles.Count - 1; i >= 0; i--)
            {
                NetworkProjectile projectile = orbitingProjectiles[i];
                if (projectile == null || projectile.NetworkObject == null || !projectile.NetworkObject.IsSpawned)
                    continue;

                projectile.DetachFromOrbit();
            }

            orbitingProjectiles.Clear();
        }

        private Vector3 GetInitialDirection(Vector3 tangent, float rotSpeed, float radius)
        {
            float tangentSpeed = Mathf.Abs(rotSpeed) * Mathf.Deg2Rad * Mathf.Max(0f, radius);
            if (tangentSpeed <= 0.01f)
                tangentSpeed = Mathf.Max(0.1f, center.Speed);

            Vector3 velocity = tangent.normalized * tangentSpeed;
            return velocity.sqrMagnitude > 0.0001f ? velocity.normalized : center.Direction;
        }
    }
}
