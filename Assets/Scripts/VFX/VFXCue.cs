using System;
using UnityEngine;

namespace Vamsurlike.VFX
{
    [Serializable]
    public struct VFXCue
    {
        public int cueId;
        public Vector3 position;
        public Vector3 direction;
        public float radius;
        public float duration;
        public Color color;
        public ulong targetNetworkObjectId;

        public VFXCue(
            int cueId,
            Vector3 position,
            Vector3 direction,
            float radius,
            float duration,
            Color color,
            ulong targetNetworkObjectId = ulong.MaxValue)
        {
            this.cueId = cueId;
            this.position = position;
            this.direction = direction;
            this.radius = radius;
            this.duration = duration;
            this.color = color;
            this.targetNetworkObjectId = targetNetworkObjectId;
        }
    }
}
