using System.Collections.Generic;
using UnityEngine;

namespace Vamsurlike.VFX
{
    // 로컬 카메라 전용 쉐이크 적용기. Main Camera에 배치한다.
    // 이 카메라는 CinemachineBrain이 매 프레임 LateUpdate에서 위치를 다시 계산해 덮어쓰므로,
    // "원래 위치 캐시 + 오프셋" 방식은 Cinemachine과 충돌한다 — 대신 Cinemachine이 이미 갱신을
    // 끝낸 뒤(DefaultExecutionOrder로 후순위 보장) 그 프레임의 실제 위치 위에 오프셋을 더하는
    // 방식으로 적용한다. 여러 CameraShakeCue가 동시에 들어오면 감쇠 가중 오프셋을 합산한다.
    // LevelingUp/ChestOpening처럼 Time.timeScale=0 상태에서도 동작해야 하는 연출이 있어
    // unscaledDeltaTime 기준으로 진행한다 (GAME_PLAN §8.5 카메라 쉐이크 규칙).
    [DefaultExecutionOrder(10000)]
    public sealed class CameraShakeListener : MonoBehaviour
    {
        [SerializeField] private CameraShakeEventSO shakeEvent;
        [SerializeField] private float positionMultiplier = 0.3f;
        [SerializeField] private float maxOffset = 1.5f;     // 다중 쉐이크 합산 시 폭주 방지용 클램프

        private struct ActiveShake
        {
            public Vector3 Position;
            public float   Intensity;
            public float   Duration;
            public float   Radius;
            public float   Elapsed;
        }

        private readonly List<ActiveShake> activeShakes = new();

        private void OnEnable()
        {
            if (shakeEvent != null) shakeEvent.Raised += OnShake;
        }

        private void OnDisable()
        {
            if (shakeEvent != null) shakeEvent.Raised -= OnShake;
            activeShakes.Clear();
        }

        private void OnShake(CameraShakeCue cue)
        {
            activeShakes.Add(new ActiveShake
            {
                Position  = cue.Position,
                Intensity = cue.Intensity,
                Duration  = Mathf.Max(0.01f, cue.Duration),
                Radius    = Mathf.Max(0.01f, cue.Radius),
                Elapsed   = 0f,
            });
        }

        private void LateUpdate()
        {
            if (activeShakes.Count == 0) return;

            float   dt     = Time.unscaledDeltaTime;
            Vector3 offset = Vector3.zero;

            for (int i = activeShakes.Count - 1; i >= 0; i--)
            {
                ActiveShake shake = activeShakes[i];
                shake.Elapsed += dt;
                if (shake.Elapsed >= shake.Duration)
                {
                    activeShakes.RemoveAt(i);
                    continue;
                }

                // 거리 감쇠: 진원지에서 반경(Radius) 밖이면 0, 진원지에 가까울수록 1에 수렴.
                float distance = Vector3.Distance(transform.position, shake.Position);
                float distanceAtten = Mathf.Clamp01(1f - distance / shake.Radius);

                // 시간 감쇠: 시작 직후가 가장 강하고 지속시간 끝에서 0으로 선형 감소.
                float timeAtten = 1f - shake.Elapsed / shake.Duration;

                float strength = shake.Intensity * distanceAtten * timeAtten;
                offset += Random.insideUnitSphere * strength * positionMultiplier;

                activeShakes[i] = shake;
            }

            // Cinemachine이 이번 프레임에 이미 배치를 끝낸 transform.position 위에 그대로 더한다
            // (원래 위치를 캐시해 두지 않는다 — 팔로우캠이라 매 프레임 기준 위치 자체가 바뀐다).
            offset = Vector3.ClampMagnitude(offset, maxOffset);
            transform.position += offset;
        }
    }
}
