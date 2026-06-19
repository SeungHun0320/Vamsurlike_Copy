using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Items;

namespace Vamsurlike.Stage
{
    internal static class DebugItemSpawnCommands
    {
        private const float SpawnSpacing = 1.5f;

        public static int SpawnItems(ItemDataSO[] items, Vector3 startPosition)
        {
            if (items == null || items.Length == 0)
            {
                Debug.LogWarning($"[{nameof(DebugItemSpawnCommands)}] No debug items assigned.");
                return 0;
            }

            int spawned = 0;
            Vector3 spawnPosition = startPosition;
            for (int i = 0; i < items.Length; i++)
            {
                ItemDataSO item = items[i];
                if (item == null) continue;

                bool ok = NetworkedItemPickup.SpawnAt(item, spawnPosition);
                if (ok) spawned++;

                Debug.Log($"[{nameof(DebugItemSpawnCommands)}] Spawn {(ok ? "ok" : "failed")}: {item.name} @ {spawnPosition}");
                spawnPosition += Vector3.right * SpawnSpacing;
            }

            return spawned;
        }
    }
}
