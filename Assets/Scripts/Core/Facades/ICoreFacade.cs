using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Core
{
    public interface ICoreFacade
    {
        void  PlaySFX(AudioClip clip, Vector3 pos = default);
        void  PlayBGM(AudioClip clip, float fadeTime = 1f);
        void  SetBGMVolume(float value);
        void  SetSFXVolume(float value);
        void  LoadScene(string sceneName);
        void  SaveSettings();
        GameSettings LoadSettings();
        T     GetFromPool<T>(string key) where T : Component;
        void  ReturnToPool<T>(string key, T obj) where T : Component;
        float GetGameTime();
    }
}
