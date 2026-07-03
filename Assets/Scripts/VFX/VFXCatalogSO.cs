using System;
using UnityEngine;

namespace Vamsurlike.VFX
{
    [CreateAssetMenu(fileName = "VFXCatalog", menuName = "Vamsurlike/VFX/VFX Catalog")]
    public sealed class VFXCatalogSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public int cueId;
            public string label;
            public GameObject prefab;
        }

        [SerializeField] private Entry[] entries;

        public bool TryGetPrefab(int cueId, out GameObject prefab)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].cueId != cueId) continue;
                    prefab = entries[i].prefab;
                    return prefab != null;
                }
            }

            prefab = null;
            return false;
        }
    }
}
