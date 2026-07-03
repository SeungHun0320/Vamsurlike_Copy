using UnityEngine;

namespace Vamsurlike.VFX
{
    public sealed class VFXLifetime : MonoBehaviour
    {
        [SerializeField] private float defaultDuration = 1f;

        private PooledVFX pooledVFX;
        private float duration;
        private float elapsed;
        private bool running;

        private void Awake()
        {
            pooledVFX = GetComponent<PooledVFX>();
        }

        private void OnEnable()
        {
            SetDuration(defaultDuration);
        }

        public void SetDuration(float seconds)
        {
            duration = seconds > 0f ? seconds : defaultDuration;
            elapsed = 0f;
            running = duration > 0f;
        }

        private void Update()
        {
            if (!running) return;

            elapsed += Time.deltaTime;
            if (elapsed < duration) return;

            running = false;
            if (pooledVFX != null) pooledVFX.ReturnToPool();
            else Destroy(gameObject);
        }
    }
}
