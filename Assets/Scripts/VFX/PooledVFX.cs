using UnityEngine;
using Vamsurlike.Network;

namespace Vamsurlike.VFX
{
    public sealed class PooledVFX : MonoBehaviour
    {
        private GameObject sourcePrefab;
        private bool returned;

        public void Initialize(GameObject prefab)
        {
            sourcePrefab = prefab;
            returned = false;
        }

        private void OnDisable()
        {
            returned = false;
        }

        public void ReturnToPool()
        {
            if (returned) return;
            returned = true;

            if (sourcePrefab != null && PoolManager.Instance != null)
            {
                PoolManager.Instance.ReturnGO(sourcePrefab, gameObject);
                return;
            }

            Destroy(gameObject);
        }
    }
}
