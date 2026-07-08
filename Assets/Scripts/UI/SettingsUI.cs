using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.Core;

namespace Vamsurlike.UI
{
    // MainMenu 씬(또는 게임 중 팝업)에 배치.
    // SettingsManager와 양방향 바인딩 — 패널이 열릴 때 현재 값으로 초기화된다.
    //
    // 필수 자식 구조:
    //   MasterSlider   (Slider)
    //   BgmSlider      (Slider)
    //   SfxSlider      (Slider)
    //   ResolutionDropdown (TMP_Dropdown)
    //   FullscreenToggle   (Toggle)
    //   FrameCapDropdown   (TMP_Dropdown)
    //   CloseButton        (Button) — 선택
    public class SettingsUI : MonoBehaviour
    {
        [SerializeField] private Slider      masterSlider;
        [SerializeField] private Slider      bgmSlider;
        [SerializeField] private Slider      sfxSlider;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle      fullscreenToggle;
        [SerializeField] private TMP_Dropdown frameCapDropdown;
        [SerializeField] private Button      closeButton;
        [SerializeField] private Button      openButton;
        [SerializeField] private CanvasGroup canvasGroup;

        private bool isInitializing;

        // 패널 GameObject는 항상 활성 상태로 유지한다. 비활성 상태로 씬에 저장되면
        // Awake()가 활성화 전까지 실행되지 않아(Unity의 비활성 오브젝트 규칙),
        // openButton 리스너 배선 자체가 이뤄지지 않아 버튼이 아예 반응하지 않게 된다.
        // 표시/숨김은 CanvasGroup으로만 제어한다.
        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
            if (openButton  != null) openButton.onClick.AddListener(OpenPanel);

            if (SettingsManager.Instance == null)
            {
                Debug.LogError($"[{nameof(SettingsUI)}] SettingsManager.Instance가 없습니다. Bootstrap 씬이 먼저 로드되었는지 확인하세요.", this);
            }
            else
            {
                BuildDropdowns();
                BindListeners();
            }

            HidePanel();
        }

        private void OnDestroy()
        {
            UnbindListeners();
        }

        public void OpenPanel()
        {
            if (SettingsManager.Instance != null) LoadCurrentValues();
            ShowPanel();
        }

        public void ClosePanel() => HidePanel();

        private void ShowPanel()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        private void HidePanel()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        // ─── 초기화 ───────────────────────────────────────────────────
        private void BuildDropdowns()
        {
            // 해상도 목록
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                var options = new List<string>();
                foreach (var r in SettingsManager.Instance.GetResolutions())
                    options.Add($"{r.width} × {r.height}  {r.refreshRateRatio.value:F0}Hz");
                resolutionDropdown.AddOptions(options);
            }

            // 프레임 캡 목록
            if (frameCapDropdown != null)
            {
                frameCapDropdown.ClearOptions();
                frameCapDropdown.AddOptions(new List<string>(SettingsManager.FrameCapLabels));
            }
        }

        private void LoadCurrentValues()
        {
            isInitializing = true;

            var s = SettingsManager.Instance.Current;
            if (masterSlider  != null) masterSlider.value  = s.masterVolume;
            if (bgmSlider     != null) bgmSlider.value     = s.bgmVolume;
            if (sfxSlider     != null) sfxSlider.value     = s.sfxVolume;
            if (fullscreenToggle != null) fullscreenToggle.isOn = s.isFullscreen;

            if (resolutionDropdown != null)
                resolutionDropdown.value = Mathf.Clamp(
                    s.resolutionIndex, 0, resolutionDropdown.options.Count - 1);

            if (frameCapDropdown != null)
                frameCapDropdown.value = Mathf.Clamp(
                    s.frameCapIndex, 0, frameCapDropdown.options.Count - 1);

            isInitializing = false;
        }

        // ─── 이벤트 바인딩 ────────────────────────────────────────────
        private void BindListeners()
        {
            if (masterSlider      != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
            if (bgmSlider         != null) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            if (sfxSlider         != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            if (fullscreenToggle   != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            if (frameCapDropdown   != null) frameCapDropdown.onValueChanged.AddListener(OnFrameCapChanged);
        }

        private void UnbindListeners()
        {
            if (masterSlider      != null) masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            if (bgmSlider         != null) bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
            if (sfxSlider         != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
            if (fullscreenToggle   != null) fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
            if (frameCapDropdown   != null) frameCapDropdown.onValueChanged.RemoveListener(OnFrameCapChanged);
        }

        // ─── 콜백 ─────────────────────────────────────────────────────
        private void OnMasterChanged(float v)    { if (!isInitializing) SettingsManager.Instance.SetMasterVolume(v); }
        private void OnBgmChanged(float v)       { if (!isInitializing) SettingsManager.Instance.SetBgmVolume(v); }
        private void OnSfxChanged(float v)       { if (!isInitializing) SettingsManager.Instance.SetSfxVolume(v); }
        private void OnFrameCapChanged(int i)    { if (!isInitializing) SettingsManager.Instance.SetFrameCap(i); }

        private void OnResolutionChanged(int i)
        {
            if (isInitializing) return;
            bool fs = fullscreenToggle != null && fullscreenToggle.isOn;
            SettingsManager.Instance.SetResolution(i, fs);
        }

        private void OnFullscreenChanged(bool fs)
        {
            if (isInitializing) return;
            SettingsManager.Instance.SetFullscreen(fs);
        }
    }
}
