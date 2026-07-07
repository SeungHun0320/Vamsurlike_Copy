using System;
using UnityEngine;

namespace Vamsurlike.Audio
{
    [CreateAssetMenu(fileName = "SFXCatalog", menuName = "Vamsurlike/Audio/SFX Catalog")]
    public sealed class SFXCatalogSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public int cueId;
            public string label;
            public AudioClip clip;
            [Range(0f, 2f)] public float volume;
            [Range(0f, 0.5f)] public float pitchVariance;
        }

        [SerializeField] private Entry[] entries;

        public bool TryGetEntry(int cueId, out Entry entry)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].cueId != cueId) continue;
                    entry = entries[i];
                    return entry.clip != null;
                }
            }

            entry = default;
            return false;
        }
    }
}
