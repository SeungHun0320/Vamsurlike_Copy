using UnityEngine;
using Vamsurlike.Network;
using Vamsurlike.VFX;

namespace Vamsurlike.Skills
{
    // 근접 공격 범위를 부채꼴(sector)로 바닥에 표시.
    // SkillVFXController.ShowMeleeClientRpc가 PoolManager를 거쳐 생성한다.
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

        // 풀에서 꺼내졌을 때만 채워짐 — 있으면 만료 시 Destroy 대신 풀로 반환한다.
        private GameObject sourcePrefab;

        private void Awake()
        {
            lr = GetComponent<LineRenderer>();
            lr.useWorldSpace   = false;
            lr.loop            = false;
            lr.widthMultiplier = LineWidth;

            if (lineMaterial != null)
                lr.sharedMaterial = lineMaterial;
            else if (lr.sharedMaterial == null)
                lr.sharedMaterial = LineDefaultMaterial.Get();
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
                ReturnOrDestroySelf();
        }

        private void ReturnOrDestroySelf()
        {
            if (sourcePrefab != null)
                PoolManager.ReturnOrDestroyGO(sourcePrefab, gameObject, nameof(MeleeArcVFX));
            else
                Destroy(gameObject);
        }

        // ── 스태틱 팩토리 ──────────────────────────────────────────
        // prefab이 있으면 PoolManager를 거쳐 꺼내고, 없으면(미배선 시) 직접 생성해 하위 호환한다.
        public static void Spawn(GameObject prefab, Vector3 origin, Vector3 flatForward, float range, float halfAngleDeg, float lifeTime)
        {
            Vector3 position = origin + Vector3.up * HeightOffset;
            Quaternion rotation = Quaternion.LookRotation(flatForward, Vector3.up);

            GameObject go;
            MeleeArcVFX vfx;
            if (prefab != null)
            {
                go = PoolManager.GetOrInstantiateGO(prefab, position, rotation, nameof(MeleeArcVFX));
                if (go == null) return;
                if (!go.TryGetComponent(out vfx))
                {
                    Debug.LogWarning($"[{nameof(MeleeArcVFX)}] prefab에 MeleeArcVFX 컴포넌트가 없습니다. prefab={prefab.name}");
                    PoolManager.ReturnOrDestroyGO(prefab, go, nameof(MeleeArcVFX));
                    return;
                }
            }
            else
            {
                go = new GameObject("MeleeArcVFX");
                go.transform.SetPositionAndRotation(position, rotation);
                go.AddComponent<LineRenderer>();
                vfx = go.AddComponent<MeleeArcVFX>();
            }

            vfx.sourcePrefab = prefab;
            vfx.Initialize(range, halfAngleDeg, lifeTime);
        }
    }
}
