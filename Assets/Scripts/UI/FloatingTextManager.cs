using UnityEngine;
using Vamsurlike.Network;

namespace Vamsurlike.UI
{
    public class FloatingTextManager : MonoBehaviour
    {
        public static FloatingTextManager Instance { get; private set; }

        [SerializeField] private FloatingText floatingTextPrefab;
        [SerializeField] private Color damageColor = Color.white;
        [SerializeField] private Color criticalColor = Color.yellow;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ShowDamage(float damage, Vector3 worldPosition, bool isCrit = false)
        {
            if (floatingTextPrefab == null)
            {
                Debug.LogWarning($"[{nameof(FloatingTextManager)}] floatingTextPrefab is not assigned.");
                return;
            }

            Color color = isCrit ? criticalColor : damageColor;
            GameObject prefab = floatingTextPrefab.gameObject;
            GameObject go = PoolManager.Instance != null
                ? PoolManager.Instance.GetGO(prefab, worldPosition, Quaternion.identity)
                : Instantiate(prefab, worldPosition, Quaternion.identity);

            if (!go.TryGetComponent<FloatingText>(out var text))
            {
                Debug.LogWarning($"[{nameof(FloatingTextManager)}] Spawned prefab has no {nameof(FloatingText)} component.");
                if (PoolManager.Instance != null)
                    PoolManager.Instance.ReturnGO(prefab, go);
                else
                    Destroy(go);
                return;
            }

            text.Initialize(damage, worldPosition, color, prefab);
        }
    }
}
