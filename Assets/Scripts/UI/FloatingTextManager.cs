using UnityEngine;

namespace Vamsurlike.UI
{
    public class FloatingTextManager : MonoBehaviour
    {
        public static FloatingTextManager Instance { get; private set; }

        [SerializeField] private FloatingText floatingTextPrefab;
        [SerializeField] private Color damageColor = Color.white;
        [SerializeField] private Color criticalColor = Color.yellow;
        [SerializeField] private float criticalThreshold = 50f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ShowDamage(float damage, Vector3 worldPosition)
        {
            if (floatingTextPrefab == null)
            {
                Debug.LogWarning($"[{nameof(FloatingTextManager)}] floatingTextPrefab is not assigned.");
                return;
            }

            Color color = damage >= criticalThreshold ? criticalColor : damageColor;
            FloatingText text = Instantiate(floatingTextPrefab, worldPosition, Quaternion.identity);
            text.Initialize(damage, worldPosition, color);
        }
    }
}
