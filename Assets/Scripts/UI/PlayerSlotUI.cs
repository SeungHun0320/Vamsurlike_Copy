using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI
{
    // CoopHUD 팀원 슬롯 하나.
    // CoopHUDUI가 Instantiate하고 UpdateSlot()으로 상태를 갱신한다.
    public sealed class PlayerSlotUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] public Image           hpFill;
        [SerializeField] public TextMeshProUGUI nameText;
        [SerializeField] public GameObject      downedOverlay;
        [SerializeField] public TextMeshProUGUI downedTimerText;
        [SerializeField] public GameObject      deadOverlay;
        [SerializeField] public TextMeshProUGUI deadTimerText;

        public void UpdateSlot(
            PlayerStatusPayload p,
            Color aliveColor,
            Color downedColor,
            Color deadColor)
        {
            if (hpFill != null)
            {
                float t = p.MaxHp > 0f ? p.Hp / p.MaxHp : 0f;
                FilledImageUtility.SetAmount(hpFill, t);
                hpFill.color = p.IsDowned ? downedColor : p.IsAlive ? aliveColor : deadColor;
            }

            if (nameText != null)
                nameText.text = string.IsNullOrEmpty(p.DisplayName) ? $"P{p.ClientId}" : p.DisplayName;

            if (downedOverlay != null)
                downedOverlay.SetActive(p.IsDowned);

            if (downedTimerText != null && p.IsDowned)
                downedTimerText.text = $"다운됨 {p.DownedTimeRemaining:0}s";

            if (deadOverlay != null)
                deadOverlay.SetActive(p.IsDeadWaiting);

            if (deadTimerText != null && p.IsDeadWaiting)
                deadTimerText.text = $"{p.DeadWaitRemaining:0}s";
        }
    }
}
