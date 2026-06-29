using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Skills;
using Vamsurlike.UI.Events;

namespace Vamsurlike.Player
{
    // 플레이어 프리팹에 배치.
    // Owner 클라이언트에서 SkillManager.ManualCooldown* NetworkVariable을 폴링해
    // UIEventHub.Skill.UltimateCooldownChanged 발행.
    [RequireComponent(typeof(SkillManager))]
    public sealed class ManualSkillAdapter : NetworkBehaviour
    {
        private SkillManager skillManager;
        private float        lastRemaining = -1f;
        private float        lastDuration  = -1f;

        private void Awake() => skillManager = GetComponent<SkillManager>();

        private void Update()
        {
            if (!IsOwner || skillManager == null) return;
            if (UIEventHub.Instance == null) return;

            float remaining = skillManager.ManualCooldownRemaining.Value;
            float duration  = skillManager.ManualCooldownDuration.Value;

            // IsReady 상태 전환(쿨다운 완료)은 임계값 무시 — 0.0 고정 방지
            bool readyStateChanged = (remaining <= 0f) != (lastRemaining <= 0f);

            // 0.05s 미만 변화 무시 — 매 프레임 string alloc 방지
            if (!readyStateChanged && Mathf.Abs(remaining - lastRemaining) < 0.05f && duration == lastDuration) return;

            lastRemaining = remaining;
            lastDuration  = duration;

            UIEventHub.Instance.Skill.PublishUltimateCooldownChanged(
                new UltimateCooldownPayload(remaining, duration));
        }
    }
}
