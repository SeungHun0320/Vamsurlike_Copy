using System.Collections.Generic;

namespace Vamsurlike.Enemy
{
    public static class EnemyRegistry
    {
        private static readonly List<EnemyNetworkBase> active = new();

        public static IReadOnlyList<EnemyNetworkBase> All => active;

        public static void Register(EnemyNetworkBase enemy)
        {
            if (!active.Contains(enemy))
                active.Add(enemy);
        }

        public static void Unregister(EnemyNetworkBase enemy)
        {
            active.Remove(enemy);
        }
    }
}
