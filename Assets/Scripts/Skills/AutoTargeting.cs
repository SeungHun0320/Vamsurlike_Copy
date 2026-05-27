using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    public static class AutoTargeting
    {
        public static EnemyNetworkBase FindNearestEnemy(
            Vector3 origin,
            float range,
            HashSet<ulong> ignoredNetworkObjectIds = null)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return null;

            float sqrRange = range * range;
            float bestSqrDistance = float.MaxValue;
            EnemyNetworkBase best = null;

            foreach (var networkObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
            {
                if (networkObject == null) continue;
                if (!networkObject.TryGetComponent<EnemyNetworkBase>(out var enemy)) continue;
                if (!enemy.IsAlive) continue;
                if (ignoredNetworkObjectIds != null && ignoredNetworkObjectIds.Contains(networkObject.NetworkObjectId)) continue;

                float sqrDistance = SqrDistanceXZ(enemy.transform.position, origin);
                if (sqrDistance > sqrRange || sqrDistance >= bestSqrDistance) continue;

                bestSqrDistance = sqrDistance;
                best = enemy;
            }

            return best;
        }

        public static int FindEnemiesInRange(Vector3 origin, float range, List<EnemyNetworkBase> results)
        {
            if (results == null) return 0;
            results.Clear();

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return 0;

            float sqrRange = range * range;

            foreach (var networkObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
            {
                if (networkObject == null) continue;
                if (!networkObject.TryGetComponent<EnemyNetworkBase>(out var enemy)) continue;
                if (!enemy.IsAlive) continue;

                float sqrDistance = SqrDistanceXZ(enemy.transform.position, origin);
                if (sqrDistance > sqrRange) continue;

                results.Add(enemy);
            }

            return results.Count;
        }

        private static float SqrDistanceXZ(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }
    }
}
