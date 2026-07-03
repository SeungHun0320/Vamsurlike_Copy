using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.VFX
{
    public sealed class VFXEventBridge : NetworkBehaviour
    {
        [SerializeField] private VFXSpawnEventSO vfxSpawnEvent;
        [SerializeField] private CameraShakeEventSO cameraShakeEvent;
        [SerializeField] private FloatingTextEventSO floatingTextEvent;

        public void PlayVFXForAll(int cueId, Vector3 position, Vector3 direction, float radius, float duration, Color color, ulong targetNetworkObjectId = ulong.MaxValue)
        {
            if (!IsServer) return;
            PlayVFXClientRpc(cueId, position, direction, radius, duration, color, targetNetworkObjectId);
        }

        public void ShakeForAll(Vector3 position, float intensity, float duration, float radius)
        {
            if (!IsServer) return;
            ShakeClientRpc(position, intensity, duration, radius);
        }

        public void ShowFloatingTextForAll(Vector3 position, float value, Color color, bool isCritical)
        {
            if (!IsServer) return;
            ShowFloatingTextClientRpc(position, value, color, isCritical);
        }

        [ClientRpc]
        private void PlayVFXClientRpc(int cueId, Vector3 position, Vector3 direction, float radius, float duration, Color color, ulong targetNetworkObjectId)
        {
            vfxSpawnEvent?.Raise(new VFXCue(cueId, position, direction, radius, duration, color, targetNetworkObjectId));
        }

        [ClientRpc]
        private void ShakeClientRpc(Vector3 position, float intensity, float duration, float radius)
        {
            cameraShakeEvent?.Raise(new CameraShakeCue(position, intensity, duration, radius));
        }

        [ClientRpc]
        private void ShowFloatingTextClientRpc(Vector3 position, float value, Color color, bool isCritical)
        {
            floatingTextEvent?.Raise(new FloatingTextCue(position, value, color, isCritical));
        }
    }
}
