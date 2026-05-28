using TMPro;
using UnityEngine;

namespace Vamsurlike.UI
{
    public class FloatingText : MonoBehaviour
    {
        [SerializeField] private TextMeshPro textMesh;
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float lifetime = 1.0f;
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.5f);

        private float elapsed;
        private Color baseColor;

        public void Initialize(float damage, Vector3 worldPosition, Color? color = null)
        {
            transform.position = worldPosition + Vector3.up * 0.5f;
            textMesh.text = Mathf.RoundToInt(damage).ToString();
            baseColor = color ?? Color.white;
            textMesh.color = baseColor;
            elapsed = 0f;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            transform.position += Vector3.up * (moveSpeed * Time.deltaTime);

            if (Camera.main != null)
                transform.forward = Camera.main.transform.forward;

            Color c = baseColor;
            c.a = alphaCurve.Evaluate(t);
            textMesh.color = c;

            float scale = scaleCurve.Evaluate(t);
            transform.localScale = Vector3.one * scale;

            if (elapsed >= lifetime)
                Destroy(gameObject);
        }
    }
}
