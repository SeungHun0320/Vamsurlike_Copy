using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Core
{
    public interface ICoreFacade
    {
        void PlaySFX(AudioClip clip, Vector3 pos = default);
        void PlayBGM(AudioClip clip, float fadeTime = 1f);
        void LoadScene(string sceneName);
        void SaveSettings(GameSettings settings);
        GameSettings LoadSettings();
        T GetFromPool<T>(string key) where T : Component;
        void ReturnToPool<T>(string key, T obj) where T : Component;
    }
}
