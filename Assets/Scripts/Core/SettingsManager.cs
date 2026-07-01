using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Core
{
    // Bootstrap 씬에 배치. DontDestroyOnLoad.
    // 설정을 PlayerPrefs(JSON)에 저장·불러오고 즉시 적용한다.
    // 오디오 적용은 AudioManager 구현 후 OnVolumeChanged 이벤트를 구독해 처리한다.
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        private const string PrefKey = "Vamsurlike.GameSettings";

        public static readonly string[] FrameCapLabels = { "60", "120", "무제한" };
        private static readonly int[]   FrameCapValues = { 60, 120, -1 };

        public GameSettings Current { get; private set; } = new();

        // AudioManager가 구독해 볼륨 반영
        public System.Action<float, float, float> OnVolumeChanged;

        private Resolution[] cachedResolutions;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        private void Start()
        {
            ApplyFrameCap(Current.frameCapIndex);
            ApplyResolution(Current.resolutionIndex, Current.isFullscreen);
            NotifyVolume();
        }

        // ─── 볼륨 ────────────────────────────────────────────────────
        public void SetMasterVolume(float value)
        {
            Current.masterVolume = value;
            NotifyVolume();
            Save();
        }

        public void SetBgmVolume(float value)
        {
            Current.bgmVolume = value;
            NotifyVolume();
            Save();
        }

        public void SetSfxVolume(float value)
        {
            Current.sfxVolume = value;
            NotifyVolume();
            Save();
        }

        // ─── 해상도 ───────────────────────────────────────────────────
        public Resolution[] GetResolutions()
        {
            if (cachedResolutions == null || cachedResolutions.Length == 0)
                cachedResolutions = Screen.resolutions;
            return cachedResolutions;
        }

        public void SetResolution(int index, bool fullscreen)
        {
            Current.resolutionIndex = index;
            Current.isFullscreen    = fullscreen;
            ApplyResolution(index, fullscreen);
            Save();
        }

        public void SetFullscreen(bool fullscreen)
        {
            Current.isFullscreen = fullscreen;
            ApplyResolution(Current.resolutionIndex, fullscreen);
            Save();
        }

        // ─── 프레임 캡 ────────────────────────────────────────────────
        public void SetFrameCap(int index)
        {
            Current.frameCapIndex = index;
            ApplyFrameCap(index);
            Save();
        }

        // ─── 내부 ─────────────────────────────────────────────────────
        private void ApplyFrameCap(int index)
        {
            int clamped = Mathf.Clamp(index, 0, FrameCapValues.Length - 1);
            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = FrameCapValues[clamped];
        }

        private void ApplyResolution(int index, bool fullscreen)
        {
            var resolutions = GetResolutions();
            if (resolutions.Length == 0) return;

            int clamped = Mathf.Clamp(index, 0, resolutions.Length - 1);
            Resolution r = resolutions[clamped];
            Screen.SetResolution(r.width, r.height, fullscreen);
        }

        private void NotifyVolume()
        {
            OnVolumeChanged?.Invoke(Current.masterVolume, Current.bgmVolume, Current.sfxVolume);
        }

        private void Save()
        {
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(Current));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            string json = PlayerPrefs.GetString(PrefKey, string.Empty);
            Current = string.IsNullOrEmpty(json) ? new GameSettings() : JsonUtility.FromJson<GameSettings>(json);
        }
    }
}
