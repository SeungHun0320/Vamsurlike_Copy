using System;
using UnityEngine;

namespace Vamsurlike.VFX
{
    [CreateAssetMenu(fileName = "GameEvent", menuName = "Vamsurlike/VFX/Game Event")]
    public sealed class GameEventSO : ScriptableObject
    {
        public event Action Raised;

        public void Raise()
        {
            Raised?.Invoke();
        }
    }
}
