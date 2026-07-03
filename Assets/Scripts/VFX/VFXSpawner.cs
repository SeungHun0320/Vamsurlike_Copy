using UnityEngine;
using Vamsurlike.Network;

namespace Vamsurlike.VFX
{
    public sealed class VFXSpawner : MonoBehaviour
    {
        [SerializeField] private VFXSpawnEventSO spawnEvent;
        [SerializeField] private VFXCatalogSO catalog;
        [SerializeField] private bool rotateToDirection = true;

        private void OnEnable()
        {
            if (spawnEvent != null)
                spawnEvent.Raised += Spawn;
        }

        private void OnDisable()
        {
            if (spawnEvent != null)
                spawnEvent.Raised -= Spawn;
        }

        private void Spawn(VFXCue cue)
        {
            if (catalog == null || !catalog.TryGetPrefab(cue.cueId, out GameObject prefab))
            {
                Debug.LogWarning($"[{nameof(VFXSpawner)}] VFX prefab not found. cueId={cue.cueId}", this);
                return;
            }

            Quaternion rotation = Quaternion.identity;
            if (rotateToDirection && cue.direction.sqrMagnitude > 0.0001f)
                rotation = Quaternion.LookRotation(cue.direction.normalized, Vector3.up);

            GameObject instance = PoolManager.Instance != null
                ? PoolManager.Instance.GetGO(prefab, cue.position, rotation)
                : Instantiate(prefab, cue.position, rotation);

            if (instance == null) return;
            instance.transform.SetPositionAndRotation(cue.position, rotation);

            if (!instance.TryGetComponent(out PooledVFX pooledVFX))
                pooledVFX = instance.AddComponent<PooledVFX>();
            pooledVFX.Initialize(prefab);

            if (instance.TryGetComponent(out VFXLifetime lifetime))
                lifetime.SetDuration(cue.duration);

            PlayParticles(instance);
        }

        private static void PlayParticles(GameObject instance)
        {
            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
                particles[i].Play(true);
        }
    }
}
