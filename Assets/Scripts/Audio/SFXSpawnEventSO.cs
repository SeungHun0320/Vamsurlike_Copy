using System;
using UnityEngine;

namespace Vamsurlike.Audio
{
    [CreateAssetMenu(fileName = "SFXSpawnEvent", menuName = "Vamsurlike/Audio/SFX Spawn Event")]
    public sealed class SFXSpawnEventSO : ScriptableObject
    {
        public event Action<SFXCue> Raised;

        public void Raise(SFXCue cue)
        {
            Raised?.Invoke(cue);
        }
    }
}
