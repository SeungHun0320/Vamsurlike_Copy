using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
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


        public static float ResolveTargetingRange(in SkillCastContext context)
        {
            if (context.LevelData == null) return 0f;

            SkillLevelData data = context.LevelData;
            float areaMultiplier = Mathf.Max(0f, context.AreaMultiplier);

            return context.Skill != null
                ? context.Skill.castType switch
                {
                    SkillCastType.AreaAura => ResolveAreaRange(data, areaMultiplier),
                    SkillCastType.Orbital => data.orbitalRadius * areaMultiplier,
                    SkillCastType.Grenade => data.grenadeRange * areaMultiplier,
                    SkillCastType.Melee => data.meleeRange * areaMultiplier,
                    SkillCastType.ClusterGrenade => data.grenadeRange * areaMultiplier,
                    SkillCastType.BlackHole => ResolveAreaRange(data, areaMultiplier),
                    SkillCastType.PierceShotgun => data.range * areaMultiplier,
                    SkillCastType.Earthshatter => data.meleeRange * areaMultiplier,
                    SkillCastType.Shotgun => data.range * areaMultiplier,
                    _ => data.range,
                }
                : data.range;
        }

        private static float ResolveAreaRange(SkillLevelData data, float areaMultiplier)
        {
            float radius = data.areaRadius > 0f ? data.areaRadius : data.range;
            return radius * areaMultiplier;
        }

        public static EnemyNetworkBase FindNearestEnemy(
            in SkillCastContext context,
            Vector3 origin,
            HashSet<ulong> ignoredNetworkObjectIds = null)
        {
            return FindNearestEnemy(origin, ResolveTargetingRange(context), ignoredNetworkObjectIds);
        }

        public static Vector3 ResolveDirection(
            in SkillCastContext context,
            Vector3 origin,
            Vector3 fallback,
            out EnemyNetworkBase target)
        {
            target = FindNearestEnemy(context, origin);
            if (target == null) return fallback;

            Vector3 direction = target.transform.position - origin;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : fallback;
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
