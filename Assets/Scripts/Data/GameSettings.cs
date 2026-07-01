using System;

namespace Vamsurlike.Data
{
    [Serializable]
    public class GameSettings
    {
        public float masterVolume    = 1f;
        public float bgmVolume       = 1f;
        public float sfxVolume       = 1f;
        public int   resolutionIndex = 0;
        public bool  isFullscreen    = true;
        public int   frameCapIndex   = 0;   // 0=60fps, 1=120fps, 2=무제한
    }
}
