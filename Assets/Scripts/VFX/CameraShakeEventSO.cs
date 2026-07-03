using System;
using UnityEngine;

namespace Vamsurlike.VFX
{
    [Serializable]
    public readonly struct CameraShakeCue
    {
        public readonly Vector3 Position;
        public readonly float Intensity;
        public readonly float Duration;
        public readonly float Radius;

        public CameraShakeCue(Vector3 position, float intensity, float duration, float radius)
        {
            Position = position;
            Intensity = intensity;
            Duration = duration;
            Radius = radius;
        }
    }

    [CreateAssetMenu(fileName = "CameraShakeEvent", menuName = "Vamsurlike/VFX/Camera Shake Event")]
    public sealed class CameraShakeEventSO : ScriptableObject
    {
        public event Action<CameraShakeCue> Raised;

        public void Raise(CameraShakeCue cue)
        {
            Raised?.Invoke(cue);
        }
    }
}
