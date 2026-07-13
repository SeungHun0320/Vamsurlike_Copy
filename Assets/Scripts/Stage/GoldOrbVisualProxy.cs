using System;
using System.Collections;
using UnityEngine;

namespace Vamsurlike.Stage
{
    // NetworkObject가 아닌 로컬 비주얼 전용 컴포넌트. XPOrbVisualProxy와 동일한 역할이지만
    // 골드는 XP와 책임(개인 귀속 vs 공유 풀)이 달라 별도 클래스로 유지한다.
    // GoldOrbManager가 ClientRpc로 생성 시 ID를 주입한다.
    public class GoldOrbVisualProxy : MonoBehaviour
    {
        // 프리팹이 아니라 GoldOrbManager가 AddComponent로 생성하는 컴포넌트라 인스펙터가 없다 —
        // 값 튜닝은 GoldOrbManager의 flyDuration/flyEase 필드(인스펙터에 노출됨)에서 하고,
        // 여기 기본값은 ConfigureFly가 호출되지 않았을 때(이론상 없음)를 위한 안전값이다.
        private float flyDuration = 0.6f;
        private AnimationCurve flyEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public ulong OrbId { get; private set; }

        private Coroutine flyCoroutine;

        public void Initialize(ulong orbId)
        {
            OrbId = orbId;
        }

        // GoldOrbManager 인스펙터 값으로 비행 연출을 튜닝할 수 있도록 주입.
        public void ConfigureFly(float duration, AnimationCurve ease)
        {
            flyDuration = Mathf.Max(0.01f, duration);
            if (ease != null) flyEase = ease;
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
