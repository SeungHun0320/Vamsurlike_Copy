using UnityEngine;
using UnityEngine.Events;

namespace Vamsurlike.VFX
{
    public sealed class EventListener : MonoBehaviour
    {
        [SerializeField] private GameEventSO eventChannel;
        [SerializeField] private UnityEvent response;

        private void OnEnable()
        {
            if (eventChannel != null)
                eventChannel.Raised += OnRaised;
        }

        private void OnDisable()
        {
            if (eventChannel != null)
                eventChannel.Raised -= OnRaised;
        }

        private void OnRaised()
        {
            response?.Invoke();
        }
    }
}
