using UnityEngine;
using Vamsurlike.Network;

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

        // 풀에서 꺼내졌을 때만 채워짐 — 있으면 만료 시 Destroy 대신 풀로 반환한다.
        private GameObject sourcePrefab;

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

        public void Initialize(float radius, float lifeTime, Color color, Transform target = null, GameObject prefab = null)
        {
            followTarget = target;
            duration = lifeTime;
            elapsed = 0f;
            hasDuration = lifeTime > 0f;
            sourcePrefab = prefab;

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
                ReturnOrDestroySelf();
        }

        private void ReturnOrDestroySelf()
        {
            if (sourcePrefab != null)
                PoolManager.ReturnOrDestroyGO(sourcePrefab, gameObject, nameof(AreaCircleVFX));
            else
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
