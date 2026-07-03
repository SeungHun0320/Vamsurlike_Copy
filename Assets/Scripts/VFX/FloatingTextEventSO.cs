using System;
using UnityEngine;

namespace Vamsurlike.VFX
{
    [Serializable]
    public readonly struct FloatingTextCue
    {
        public readonly Vector3 Position;
        public readonly float Value;
        public readonly Color Color;
        public readonly bool IsCritical;

        public FloatingTextCue(Vector3 position, float value, Color color, bool isCritical)
        {
            Position = position;
            Value = value;
            Color = color;
            IsCritical = isCritical;
        }
    }

    [CreateAssetMenu(fileName = "FloatingTextEvent", menuName = "Vamsurlike/VFX/Floating Text Event")]
    public sealed class FloatingTextEventSO : ScriptableObject
    {
        public event Action<FloatingTextCue> Raised;

        public void Raise(FloatingTextCue cue)
        {
            Raised?.Invoke(cue);
        }
    }
}
