using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // Stage 씬 HUDCanvas에 배치.
    // hpFill / xpFill 은 Image(Filled, Horizontal) 방식으로 연결.
    public sealed class PlayerHUDUI : MonoBehaviour
    {
        [Header("HP")]
        [SerializeField] private Image           hpFill;
        [SerializeField] private TextMeshProUGUI hpText;
        [Header("XP / Level")]
        [SerializeField] private Image           xpFill;
        [SerializeField] private TextMeshProUGUI xpText;
        [SerializeField] private TextMeshProUGUI levelText;
        [Header("Downed")]
        [SerializeField] private GameObject      downedOverlay;
        [SerializeField] private TextMeshProUGUI downedTimerText;
        [Header("Dead (자동 부활 대기)")]
        [SerializeField] private GameObject      deadOverlay;
        [SerializeField] private TextMeshProUGUI deadTimerText;
        [Header("Formats")]
        [SerializeField] private string hpFormat       = "{0:0}/{1:0}";
        [SerializeField] private string xpFormat       = "{0:0}/{1:0}";
        [SerializeField] private string levelFormat    = "Lv.{0}";
        [SerializeField] private string downedFormat   = "{0:0.0}s";
        [SerializeField] private string deadWaitFormat = "{0:0}초 후 부활";

        private HUDViewModel viewModel;

        private void Awake()
        {
            viewModel = new HUDViewModel();
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsServer
                && !NetworkManager.Singleton.IsHost)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            viewModel.OnLocalPlayerStatus += RenderPlayerStatus;
            viewModel.OnSharedLevel       += RenderSharedLevel;
            viewModel.Bind();
            RenderPlayerStatus(viewModel.LastPlayerStatus);
            RenderSharedLevel(viewModel.LastSharedLevel);
        }

        private void OnDisable()
        {
            viewModel.OnLocalPlayerStatus -= RenderPlayerStatus;
            viewModel.OnSharedLevel       -= RenderSharedLevel;
            viewModel.Unbind();
        }

        private void OnDestroy() => viewModel?.Dispose();

        private void RenderPlayerStatus(PlayerStatusPayload p)
        {
            float maxHp = p.MaxHp > 0f ? p.MaxHp : 1f;
            FilledImageUtility.SetAmount(hpFill, p.Hp / maxHp);
            if (hpText != null) hpText.text = string.Format(hpFormat, p.Hp, p.MaxHp);

            // 1단계: 다운 (팀원 부활 가능)
            if (downedOverlay != null) downedOverlay.SetActive(p.IsDowned);
            if (downedTimerText != null)
            {
                downedTimerText.gameObject.SetActive(p.IsDowned && p.DownedTimeRemaining > 0f);
                if (p.IsDowned) downedTimerText.text = string.Format(downedFormat, p.DownedTimeRemaining);
            }

            // 2단계: 사망 대기 (자동 부활)
            if (deadOverlay != null) deadOverlay.SetActive(p.IsDeadWaiting);
            if (deadTimerText != null)
            {
                deadTimerText.gameObject.SetActive(p.IsDeadWaiting && p.DeadWaitRemaining > 0f);
                if (p.IsDeadWaiting) deadTimerText.text = string.Format(deadWaitFormat, p.DeadWaitRemaining);
            }
        }

        private void RenderSharedLevel(SharedLevelPayload p)
        {
            FilledImageUtility.SetAmount(xpFill, p.NormalizedXp);
            if (xpText    != null) xpText.text       = string.Format(xpFormat, p.Xp, p.XpRequired);
            if (levelText != null) levelText.text    = string.Format(levelFormat, p.Level);
        }
    }

}
