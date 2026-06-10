using UnityEngine;

namespace Vamsurlike.Skills
{
    // Orbital 계열 스킬의 공통 클라이언트 비주얼 관리.
    // 서버가 BroadcastXxxClientRpc → OnClientOrbitalActivated 호출 → 시각 오브젝트 생성/갱신.
    public abstract class OrbitalVisualBase : SkillBase
    {
        private readonly GameObject visualPrefab;
        private readonly float      heightOffset;

        private GameObject[] visualObjects;
        private bool         visualsActive;

        protected float VisualRadius;
        protected float VisualRotSpeed;

        protected OrbitalVisualBase(GameObject visualPrefab, float heightOffset)
        {
            this.visualPrefab = visualPrefab;
            this.heightOffset = heightOffset;
        }

        public void OnClientOrbitalActivated(int count, float radius, float rotSpeed, Transform ownerTransform)
        {
            if (visualPrefab == null)
            {
                Debug.LogWarning($"[{GetType().Name}] orbitalVisualPrefab 미할당.");
                return;
            }

            DestroyVisuals();
            VisualRadius   = radius;
            VisualRotSpeed = rotSpeed;
            visualObjects  = new GameObject[count];

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetOrbitalPosition(
                    ownerTransform.position + Vector3.up * heightOffset, radius, rotSpeed, i, count);
                visualObjects[i] = Object.Instantiate(visualPrefab, pos, Quaternion.identity);
            }

            visualsActive = true;
        }

        public override void OnUpdate(Transform ownerTransform)
        {
            if (!visualsActive || visualObjects == null) return;

            for (int i = 0; i < visualObjects.Length; i++)
            {
                if (visualObjects[i] == null) continue;
                visualObjects[i].transform.position = GetOrbitalPosition(
                    ownerTransform.position + Vector3.up * heightOffset,
                    VisualRadius, VisualRotSpeed, i, visualObjects.Length);
            }
        }

        public override void OnDespawn() => DestroyVisuals();

        protected void DestroyVisuals()
        {
            if (visualObjects == null) return;
            for (int i = 0; i < visualObjects.Length; i++)
                if (visualObjects[i] != null)
                    Object.Destroy(visualObjects[i]);
            visualObjects = null;
            visualsActive = false;
        }

        protected static Vector3 GetOrbitalPosition(Vector3 origin, float radius, float rotSpeed, int index, int count)
        {
            float angle = Time.time * rotSpeed + 360f / count * index;
            return origin + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * radius;
        }
    }
}
