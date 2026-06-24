using System;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Stage
{
    [Serializable]
    public struct WaveRow
    {
        public int             waveGroupId;
        public int             sequenceIndex;
        public WaveEntryData[] entries;
        [Min(0f)] public float waveDuration;
        public bool            loopFromHere;
        public string          spawnActionName;
    }

    [CreateAssetMenu(menuName = "Vamsurlike/Data/WaveTable", fileName = "WaveTable")]
    public class WaveTableSO : DataTableSO<WaveRow> { }
}
