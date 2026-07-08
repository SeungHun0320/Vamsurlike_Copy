using System;
using System.Collections;
using UnityEngine;

namespace Vamsurlike.Stage
{
    // NetworkObject가 아닌 로컬 비주얼 전용 컴포넌트.
    // XPOrbManager가 ClientRpc로 생성 시 ID를 주입한다.
    public class XPOrbVisualProxy : MonoBehaviour
    {
        [SerializeField] private float flyDuration = 0.35f;
        [SerializeField] private AnimationCurve flyEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public ulong OrbId { get; private set; }

        private Coroutine flyCoroutine;

        public void Initialize(ulong orbId)
        {
            OrbId = orbId;
        }

        public void Clear()
        {
            OrbId = 0;
            if (flyCoroutine != null)
            {
                StopCoroutine(flyCoroutine);
                flyCoroutine = null;
            }
        }

        // 수집 확정 시 즉시 사라지는 대신 플레이어 쪽으로 빨려들어가는 연출 후 onComplete 호출.
        // target이 null이면(참조 못 찾음) fallbackPosition으로 고정 이동.
        public void FlyToAndComplete(Transform target, Vector3 fallbackPosition, Action onComplete)
        {
            if (flyCoroutine != null) StopCoroutine(flyCoroutine);
            flyCoroutine = StartCoroutine(FlyRoutine(target, fallbackPosition, onComplete));
        }

        private IEnumerator FlyRoutine(Transform target, Vector3 fallbackPosition, Action onComplete)
        {
            Vector3 start = transform.position;
            float elapsed = 0f;

            while (elapsed < flyDuration)
            {
                elapsed += Time.deltaTime;
                float t = flyEase.Evaluate(Mathf.Clamp01(elapsed / flyDuration));
                Vector3 currentTarget = target != null ? target.position : fallbackPosition;
                transform.position = Vector3.Lerp(start, currentTarget, t);
                yield return null;
            }

            flyCoroutine = null;
            onComplete?.Invoke();
        }
    }
}
