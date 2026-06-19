using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Enemy;

namespace Vamsurlike.Stage
{
    internal static class DebugEnemyCommands
    {
        private const float DebugKillDamage = 9999999f;

        public static void CollectVisibleEnemyIds(List<ulong> results)
        {
            results.Clear();

            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning($"[{nameof(DebugEnemyCommands)}] MainCamera is missing.");
                return;
            }

            EnemyNetworkBase[] enemies = Object.FindObjectsByType<EnemyNetworkBase>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyNetworkBase enemy = enemies[i];
                if (enemy == null || !enemy.IsSpawned || !enemy.IsAlive) continue;

                Vector3 viewport = camera.WorldToViewportPoint(enemy.transform.position);
                if (viewport.z <= 0f) continue;
                if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;

                results.Add(enemy.NetworkObjectId);
            }
        }

        public static int KillEnemiesByIds(NetworkManager networkManager, ulong[] enemyNetworkObjectIds)
        {
            if (networkManager == null || enemyNetworkObjectIds == null) return 0;

            int killed = 0;
            for (int i = 0; i < enemyNetworkObjectIds.Length; i++)
            {
                ulong id = enemyNetworkObjectIds[i];
                if (!networkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject obj)) continue;
                if (!obj.TryGetComponent(out EnemyNetworkBase enemy)) continue;
                if (!enemy.IsAlive) continue;

                enemy.TakeDamage(DebugKillDamage);
                killed++;
            }

            return killed;
        }
    }
}
