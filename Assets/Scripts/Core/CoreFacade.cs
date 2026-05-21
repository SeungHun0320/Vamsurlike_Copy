using UnityEngine;
using UnityEngine.Audio;
using Vamsurlike.Data;

namespace Vamsurlike.Core
{
    public class CoreFacade : MonoBehaviour, ICoreFacade
    {
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        private GameSettings settings      = new GameSettings();
        private float        gameStartTime;

        private void Awake()
        {
            gameStartTime = Time.time;
            settings      = LoadSettings();
            ApplySettings();
        }

        // ── Audio ──────────────────────────────────────────────────────────

        public void PlaySFX(AudioClip clip, Vector3 pos = default)
        {
            if (clip == null) return;
            if (sfxSource != null)
                sfxSource.PlayOneShot(clip);
            else
                AudioSource.PlayClipAtPoint(clip, pos);
        }

        public void PlayBGM(AudioClip clip, float fadeTime = 1f)
        {
            if (bgmSource == null) return;
            bgmSource.clip = clip;
            bgmSource.Play();
        }

        public void SetBGMVolume(float value)
        {
            settings.bgmVolume = Mathf.Clamp01(value);
            if (bgmSource != null) bgmSource.volume = settings.bgmVolume;
        }

        public void SetSFXVolume(float value)
        {
            settings.sfxVolume = Mathf.Clamp01(value);
            if (sfxSource != null) sfxSource.volume = settings.sfxVolume;
        }

        // ── Scene ──────────────────────────────────────────────────────────

        public void LoadScene(string sceneName)
        {
            SceneLoader.Load(sceneName);
        }

        // ── Settings ───────────────────────────────────────────────────────

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("BGMVolume", settings.bgmVolume);
            PlayerPrefs.SetFloat("SFXVolume", settings.sfxVolume);
            PlayerPrefs.Save();
        }

        public GameSettings LoadSettings()
        {
            return new GameSettings
            {
                bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 1f),
                sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f),
            };
        }

        // ── Pool (stub) ────────────────────────────────────────────────────

        public T GetFromPool<T>(string key) where T : Component
        {
            Debug.LogWarning($"[CoreFacade] Object pool not implemented. Key: {key}");
            return null;
        }

        public void ReturnToPool<T>(string key, T obj) where T : Component
        {
            Debug.LogWarning($"[CoreFacade] Object pool not implemented. Key: {key}");
        }

        // ── Time ───────────────────────────────────────────────────────────

        public float GetGameTime() => Time.time - gameStartTime;

        // ── Private ────────────────────────────────────────────────────────

        private void ApplySettings()
        {
            if (bgmSource != null) bgmSource.volume = settings.bgmVolume;
            if (sfxSource != null) sfxSource.volume = settings.sfxVolume;
        }
    }
}
