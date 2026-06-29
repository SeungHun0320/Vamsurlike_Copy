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
        [Header("Formats")]
        [SerializeField] private string hpFormat     = "{0:0}/{1:0}";
        [SerializeField] private string xpFormat     = "{0:0}/{1:0}";
        [SerializeField] private string levelFormat  = "Lv.{0}";
        [SerializeField] private string downedFormat = "{0:0.0}s";

        private HUDViewModel viewModel;

        private void Awake()
        {
            FilledImageUtility.ConfigureHorizontal(hpFill);
            FilledImageUtility.ConfigureHorizontal(xpFill);
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

        private void RenderPlayerStatus(PlayerStatusPayload p)
        {
            float maxHp = p.MaxHp > 0f ? p.MaxHp : 1f;
            FilledImageUtility.SetAmount(hpFill, p.Hp / maxHp);
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
            FilledImageUtility.SetAmount(xpFill, p.NormalizedXp);
            if (xpText    != null) xpText.text       = string.Format(xpFormat, p.Xp, p.XpRequired);
            if (levelText != null) levelText.text    = string.Format(levelFormat, p.Level);
        }
    }

    internal static class FilledImageUtility
    {
        private static Sprite fallbackSprite;

        public static void ConfigureHorizontal(Image image)
        {
            if (image == null) return;

            if (image.sprite == null)
                image.sprite = GetFallbackSprite();

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.fillClockwise = true;
        }

        public static void SetAmount(Image image, float amount)
        {
            if (image == null) return;

            ConfigureHorizontal(image);
            image.fillAmount = Mathf.Clamp01(amount);
        }

        public static Sprite GetOrCreateFallbackSprite() => GetFallbackSprite();

        private static Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null) return fallbackSprite;

            Texture2D texture = Texture2D.whiteTexture;
            fallbackSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            fallbackSprite.name = "Runtime UI Fill Sprite";
            fallbackSprite.hideFlags = HideFlags.HideAndDontSave;

            return fallbackSprite;
        }
    }
}
