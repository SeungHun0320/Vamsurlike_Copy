using UnityEngine;

namespace Vamsurlike.Skills
{
    // 근접 공격 범위를 부채꼴(sector)로 바닥에 표시.
    // SkillVFXController.ShowMeleeClientRpc에서 직접 생성한다.
    [RequireComponent(typeof(LineRenderer))]
    public sealed class MeleeArcVFX : MonoBehaviour
    {
        private static readonly Color ArcColor = new(1f, 0.75f, 0f, 0.85f);

        private const int ArcSegments  = 24;
        private const float LineWidth   = 0.06f;
        private const float HeightOffset = 0.05f;

        [SerializeField] private Material lineMaterial;

        private LineRenderer lr;
        private float        duration;
        private float        elapsed;

        private void Awake()
        {
            lr = GetComponent<LineRenderer>();
            lr.useWorldSpace   = false;
            lr.loop            = false;
            lr.widthMultiplier = LineWidth;

            if (lineMaterial != null)
                lr.sharedMaterial = lineMaterial;
            lr.startColor  = ArcColor;
            lr.endColor    = new Color(ArcColor.r, ArcColor.g, ArcColor.b, 0f);
        }

        public void Initialize(float radius, float halfAngleDeg, float lifeTime)
        {
            duration = lifeTime;
            elapsed  = 0f;

            // 점 배치: center → 왼쪽 끝 → 호(arc) → 오른쪽 끝 → center
            int total = 2 + ArcSegments + 1 + 1; // center + arc(+2 끝점 포함) + center
            lr.positionCount = total;

            int idx = 0;
            lr.SetPosition(idx++, Vector3.zero); // center

            for (int i = 0; i <= ArcSegments; i++)
            {
                float t     = (float)i / ArcSegments;
                float angle = Mathf.Lerp(-halfAngleDeg, halfAngleDeg, t) * Mathf.Deg2Rad;
                lr.SetPosition(idx++, new Vector3(
                    Mathf.Sin(angle) * radius,
                    0f,
                    Mathf.Cos(angle) * radius));
            }

            lr.SetPosition(idx, Vector3.zero); // center 복귀
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            // alpha fade-out
            float alpha = Mathf.Clamp01(1f - elapsed / Mathf.Max(duration, 0.001f));
            Color c = new(ArcColor.r, ArcColor.g, ArcColor.b, ArcColor.a * alpha);
            lr.startColor = c;
            lr.endColor   = new Color(c.r, c.g, c.b, 0f);

            if (elapsed >= duration)
                Destroy(gameObject);
        }


        // ── 스태틱 팩토리 ──────────────────────────────────────────
        public static void Spawn(Vector3 origin, Vector3 flatForward, float range, float halfAngleDeg, float lifeTime)
        {
            var go = new GameObject("MeleeArcVFX");
            go.transform.position = origin + Vector3.up * HeightOffset;
            go.transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);

            go.AddComponent<LineRenderer>();
            var vfx = go.AddComponent<MeleeArcVFX>();
            vfx.Initialize(range, halfAngleDeg, lifeTime);
        }
    }
}
