using UnityEngine;
using Vamsurlike.Network;
using Vamsurlike.VFX;

namespace Vamsurlike.Skills
{
    // 근접 공격 범위를 사각형(box)으로 바닥에 표시. 망치류가 원뿔(샷건)과 판정 형태를
    // 구분하기 위해 사각형 판정으로 바뀌면서, 시각적으로도 부채꼴(MeleeArcVFX) 대신 이걸 사용한다.
    // SkillVFXController.ShowMeleeBoxClientRpc가 PoolManager를 거쳐 생성한다.
    [RequireComponent(typeof(LineRenderer))]
    public sealed class MeleeBoxVFX : MonoBehaviour
    {
        private static readonly Color BoxColor = new(1f, 0.75f, 0f, 0.85f);

        private const float LineWidth    = 0.06f;
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
            lr.loop            = true;
            lr.widthMultiplier = LineWidth;

            if (lineMaterial != null)
                lr.sharedMaterial = lineMaterial;
            else if (lr.sharedMaterial == null)
                lr.sharedMaterial = LineDefaultMaterial.Get();
            lr.startColor = BoxColor;
            lr.endColor   = BoxColor;
        }

        public void Initialize(float range, float width, float lifeTime)
        {
            duration = lifeTime;
            elapsed  = 0f;

            float halfWidth = width * 0.5f;

            // 시전자 위치(0,0,0) 기준 전방 사각형 4개 모서리
            lr.positionCount = 4;
            lr.SetPosition(0, new Vector3(-halfWidth, 0f, 0f));
            lr.SetPosition(1, new Vector3(-halfWidth, 0f, range));
            lr.SetPosition(2, new Vector3(halfWidth, 0f, range));
            lr.SetPosition(3, new Vector3(halfWidth, 0f, 0f));
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            float alpha = Mathf.Clamp01(1f - elapsed / Mathf.Max(duration, 0.001f));
            Color c = new(BoxColor.r, BoxColor.g, BoxColor.b, BoxColor.a * alpha);
            lr.startColor = c;
            lr.endColor   = c;

            if (elapsed >= duration)
                ReturnOrDestroySelf();
        }

        private void ReturnOrDestroySelf()
        {
            if (sourcePrefab != null)
                PoolManager.ReturnOrDestroyGO(sourcePrefab, gameObject, nameof(MeleeBoxVFX));
            else
                Destroy(gameObject);
        }

        // ── 스태틱 팩토리 ──────────────────────────────────────────
        public static void Spawn(GameObject prefab, Vector3 origin, Vector3 flatForward, float range, float width, float lifeTime)
        {
            Vector3 position = origin + Vector3.up * HeightOffset;
            Quaternion rotation = Quaternion.LookRotation(flatForward, Vector3.up);

            GameObject go;
            MeleeBoxVFX vfx;
            if (prefab != null)
            {
                go = PoolManager.GetOrInstantiateGO(prefab, position, rotation, nameof(MeleeBoxVFX));
                if (go == null) return;
                if (!go.TryGetComponent(out vfx))
                {
                    Debug.LogWarning($"[{nameof(MeleeBoxVFX)}] prefab에 MeleeBoxVFX 컴포넌트가 없습니다. prefab={prefab.name}");
                    PoolManager.ReturnOrDestroyGO(prefab, go, nameof(MeleeBoxVFX));
                    return;
                }
            }
            else
            {
                go = new GameObject("MeleeBoxVFX");
                go.transform.SetPositionAndRotation(position, rotation);
                go.AddComponent<LineRenderer>();
                vfx = go.AddComponent<MeleeBoxVFX>();
            }

            vfx.sourcePrefab = prefab;
            vfx.Initialize(range, width, lifeTime);
        }
    }
}
