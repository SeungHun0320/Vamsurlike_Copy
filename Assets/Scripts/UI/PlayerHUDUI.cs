using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // Stage 씬 Canvas에 배치. 로컬 플레이어의 HP와 공유 XP/레벨을 표시.
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
        [Header("Formats")]
        [SerializeField] private string hpFormat     = "{0:0}/{1:0}";
        [SerializeField] private string xpFormat     = "{0:0}/{1:0}";
        [SerializeField] private string levelFormat  = "Lv.{0}";
        [SerializeField] private string downedFormat = "{0:0.0}s";

        private HUDViewModel viewModel;

        private void Awake()
        {
            viewModel = new HUDViewModel();
        }

        private void Start()
        {
            // 데디케이티드 서버에서는 HUD 전체 비활성화
            if (Unity.Netcode.NetworkManager.Singleton != null
                && Unity.Netcode.NetworkManager.Singleton.IsServer
                && !Unity.Netcode.NetworkManager.Singleton.IsHost)
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

        private void RenderPlayerStatus(PlayerStatusPayload p)
        {
            float maxHp = p.MaxHp > 0f ? p.MaxHp : 1f;
            if (hpFill != null) hpFill.fillAmount = p.Hp / maxHp;
            if (hpText != null) hpText.text       = string.Format(hpFormat, p.Hp, p.MaxHp);

            bool isDowned = p.IsDowned;
            if (downedOverlay   != null) downedOverlay.SetActive(isDowned);
            if (downedTimerText != null)
            {
                downedTimerText.gameObject.SetActive(isDowned && p.DownedTimeRemaining > 0f);
                if (isDowned) downedTimerText.text = string.Format(downedFormat, p.DownedTimeRemaining);
            }
        }

        private void RenderSharedLevel(SharedLevelPayload p)
        {
            if (xpFill    != null) xpFill.fillAmount = p.NormalizedXp;
            if (xpText    != null) xpText.text       = string.Format(xpFormat, p.Xp, p.XpRequired);
            if (levelText != null) levelText.text    = string.Format(levelFormat, p.Level);
        }
    }
}
