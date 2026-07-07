using System;
using UnityEngine;

namespace Vamsurlike.Audio
{
    [Serializable]
    public struct SFXCue
    {
        public int cueId;
        public Vector3 position;
        public float volumeScale;

        public SFXCue(int cueId, Vector3 position, float volumeScale = 1f)
        {
            this.cueId = cueId;
            this.position = position;
            this.volumeScale = volumeScale;
        }
    }
}
