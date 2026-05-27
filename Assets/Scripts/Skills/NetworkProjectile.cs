using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Network;

namespace Vamsurlike.Skills
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkProjectile : NetworkBehaviour
    {
        [SerializeField] private Vector3 visualRotationOffsetEuler;

        private GameObject sourcePrefab;
        private Vector3 direction;
        private float speed;
        private float damage;
        private float hitRadius;
        private float remainingLifetime;
        private int pierceRemaining;
        private bool hasInitialized;
        private readonly HashSet<ulong> hitEnemyIds = new();

        public void Initialize(
            GameObject projectilePrefab,
            ulong owner,
            Vector3 spawnPosition,
            Vector3 fireDirection,
            SkillLevelData levelData)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (levelData == null) return;

            sourcePrefab = projectilePrefab;
            direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : Vector3.forward;
            speed = levelData.projectileSpeed;
            damage = levelData.damage;
            hitRadius = levelData.projectileHitRadius;
            remainingLifetime = levelData.projectileLifetime;
            pierceRemaining = Mathf.Max(0, levelData.pierceCount);
            hitEnemyIds.Clear();
            hasInitialized = true;

            transform.position = spawnPosition;
            transform.rotation = GetProjectileRotation(direction);
        }

        public Quaternion GetProjectileRotation(Vector3 forward)
        {
            Vector3 safeForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            return Quaternion.LookRotation(safeForward, Vector3.up) * Quaternion.Euler(visualRotationOffsetEuler);
        }

        private void Update()
        {
            if (!IsServer || !hasInitialized) return;

            float deltaTime = Time.deltaTime;
            transform.position += direction * (speed * deltaTime);
            remainingLifetime -= deltaTime;

            if (TryHitEnemy() || remainingLifetime <= 0f)
                DespawnToPool();
        }

        private bool TryHitEnemy()
        {
            EnemyNetworkBase target = AutoTargeting.FindNearestEnemy(transform.position, hitRadius, hitEnemyIds);
            if (target == null) return false;

            hitEnemyIds.Add(target.NetworkObjectId);
            Debug.Log($"[{nameof(NetworkProjectile)}] Hit {target.name}. damage={damage}, projectile={name}, position={transform.position}, hitRadius={hitRadius}");
            target.TakeDamage(damage);

            if (pierceRemaining <= 0)
                return true;

            pierceRemaining--;
            return false;
        }

        private void DespawnToPool()
        {
            hasInitialized = false;
            hitEnemyIds.Clear();
            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(false);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (!IsServer) return;
            if (sourcePrefab != null && PoolManager.Instance != null)
                PoolManager.Instance.ReturnNetworkObject(sourcePrefab, NetworkObject);
        }
    }
}
