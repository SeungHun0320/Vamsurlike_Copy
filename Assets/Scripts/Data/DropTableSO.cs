using System;
using UnityEngine;

namespace Vamsurlike.Data
{
    [CreateAssetMenu(fileName = "DropTable_", menuName = "Vamsurlike/Data/Drop Table")]
    public class DropTableSO : ScriptableObject
    {
        [Serializable]
        public class DropEntry
        {
            public ItemDataSO item;
            [Range(0f, 1f)] public float chance = 0.1f;
        }

        public DropEntry[] entries = Array.Empty<DropEntry>();

        // 서버 전용: 가중 선택으로 드랍 아이템 결정. null = 드랍 없음.
        // roll [0,1) < 누적 chance일 때 해당 항목 반환 — 각 entry.chance가 실제 드롭 확률.
        public ItemDataSO Roll(System.Random rng)
        {
            if (entries == null) return null;

            float roll       = (float)rng.NextDouble();
            float cumulative = 0f;
            foreach (var entry in entries)
            {
                if (entry == null || entry.item == null) continue;
                cumulative += entry.chance;
                if (roll < cumulative) return entry.item;
            }
            return null;
        }
    }
}
