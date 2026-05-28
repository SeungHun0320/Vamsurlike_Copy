using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    public static class AutoTargeting
    {
        private static readonly int EnemyLayerMask = 1 << 7; // Layer 7: Enemy
        private static readonly Collider[] overlapBuffer = new Collider[256];

        public static EnemyNetworkBase FindNearestEnemy(
            Vector3 origin,
            float range,
            HashSet<ulong> ignoredNetworkObjectIds = null)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return null;

            int count = Physics.OverlapSphereNonAlloc(origin, range, overlapBuffer, EnemyLayerMask);
            float bestSqrDist = float.MaxValue;
            EnemyNetworkBase best = null;

            for (int i = 0; i < count; i++)
            {
                if (!overlapBuffer[i].TryGetComponent<EnemyNetworkBase>(out var enemy)) continue;
                if (!enemy.IsAlive) continue;
                if (ignoredNetworkObjectIds != null &&
                    ignoredNetworkObjectIds.Contains(enemy.NetworkObjectId)) continue;

                float sqrDist = SqrDistanceXZ(enemy.transform.position, origin);
                if (sqrDist >= bestSqrDist) continue;

                bestSqrDist = sqrDist;
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

            int count = Physics.OverlapSphereNonAlloc(origin, range, overlapBuffer, EnemyLayerMask);

            for (int i = 0; i < count; i++)
            {
                if (!overlapBuffer[i].TryGetComponent<EnemyNetworkBase>(out var enemy)) continue;
                if (!enemy.IsAlive) continue;
                results.Add(enemy);
            }

            return results.Count;
        }

        private static float SqrDistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
