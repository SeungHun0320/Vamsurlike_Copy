using UnityEngine;
using UnityEngine.Audio;
using Vamsurlike.Data;

namespace Vamsurlike.Core
{
    public class CoreFacade : MonoBehaviour, ICoreFacade
    {
        [SerializeField] private AudioSource m_bgmSource;
        [SerializeField] private AudioSource m_sfxSource;

        private GameSettings m_settings = new GameSettings();
        private float        m_fGameStartTime;

        private void Awake()
        {
            m_fGameStartTime = Time.time;
            m_settings = LoadSettings();
            ApplySettings();
        }

        // ── Audio ──────────────────────────────────────────────────────────

        public void PlaySFX(AudioClip clip, Vector3 pos = default)
        {
            if (clip == null) return;
            if (m_sfxSource != null)
                m_sfxSource.PlayOneShot(clip);
            else
                AudioSource.PlayClipAtPoint(clip, pos);
        }

        public void PlayBGM(AudioClip clip, float fadeTime = 1f)
        {
            if (m_bgmSource == null) return;
            m_bgmSource.clip = clip;
            m_bgmSource.Play();
        }

        public void SetBGMVolume(float value)
        {
            m_settings.m_fBGMVolume = Mathf.Clamp01(value);
            if (m_bgmSource != null) m_bgmSource.volume = m_settings.m_fBGMVolume;
        }

        public void SetSFXVolume(float value)
        {
            m_settings.m_fSFXVolume = Mathf.Clamp01(value);
            if (m_sfxSource != null) m_sfxSource.volume = m_settings.m_fSFXVolume;
        }

        // ── Scene ──────────────────────────────────────────────────────────

        public void LoadScene(string sceneName)
        {
            SceneLoader.Load(sceneName);
        }

        // ── Settings ───────────────────────────────────────────────────────

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("BGMVolume", m_settings.m_fBGMVolume);
            PlayerPrefs.SetFloat("SFXVolume", m_settings.m_fSFXVolume);
            PlayerPrefs.Save();
        }

        public GameSettings LoadSettings()
        {
            return new GameSettings
            {
                m_fBGMVolume = PlayerPrefs.GetFloat("BGMVolume", 1f),
                m_fSFXVolume = PlayerPrefs.GetFloat("SFXVolume", 1f),
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

        public float GetGameTime() => Time.time - m_fGameStartTime;

        // ── Private ────────────────────────────────────────────────────────

        private void ApplySettings()
        {
            if (m_bgmSource != null) m_bgmSource.volume = m_settings.m_fBGMVolume;
            if (m_sfxSource != null) m_sfxSource.volume = m_settings.m_fSFXVolume;
        }
    }
}
