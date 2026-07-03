using UnityEngine;

namespace Vamsurlike.Skills
{
    [RequireComponent(typeof(LineRenderer))]
    public class AreaCircleVFX : MonoBehaviour
    {
        [SerializeField] private int segments = 96;
        [SerializeField] private float lineWidth = 0.08f;
        [SerializeField] private float heightOffset = 0.05f;
        [SerializeField] private Material lineMaterial;

        private LineRenderer lineRenderer;
        private Transform followTarget;
        private float duration;
        private float elapsed;
        private bool hasDuration;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = false;
            lineRenderer.widthMultiplier = lineWidth;
            lineRenderer.positionCount = Mathf.Max(12, segments);

            if (lineMaterial != null)
                lineRenderer.sharedMaterial = lineMaterial;
        }

        public void Initialize(float radius, float lifeTime, Color color, Transform target = null)
        {
            followTarget = target;
            duration = lifeTime;
            elapsed = 0f;
            hasDuration = lifeTime > 0f;

            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();

            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            BuildCircle(Mathf.Max(0.1f, radius));
            UpdateFollowPosition();
        }

        private void Update()
        {
            UpdateFollowPosition();

            if (!hasDuration) return;

            elapsed += Time.deltaTime;
            if (elapsed >= duration)
                Destroy(gameObject);
        }

        private void BuildCircle(float radius)
        {
            int count = Mathf.Max(12, segments);
            lineRenderer.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / count;
                lineRenderer.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius));
            }
        }

        private void UpdateFollowPosition()
        {
            if (followTarget == null) return;

            Vector3 pos = followTarget.position;
            pos.y += heightOffset;
            transform.position = pos;
        }

    }
}
