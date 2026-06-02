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

        // 서버 전용: 시드 기반 랜덤으로 드랍 아이템 결정. null = 드랍 없음
        public ItemDataSO Roll(System.Random rng)
        {
            if (entries == null) return null;
            foreach (var entry in entries)
            {
                if (entry == null || entry.item == null) continue;
                if (rng.NextDouble() < entry.chance)
                    return entry.item;
            }
            return null;
        }
    }
}
