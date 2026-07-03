using System;
using UnityEngine;

namespace Vamsurlike.VFX
{
    [CreateAssetMenu(fileName = "VFXSpawnEvent", menuName = "Vamsurlike/VFX/VFX Spawn Event")]
    public sealed class VFXSpawnEventSO : ScriptableObject
    {
        public event Action<VFXCue> Raised;

        public void Raise(VFXCue cue)
        {
            Raised?.Invoke(cue);
        }
    }
}
