using System.Collections.Generic;

namespace Vamsurlike.Data.Runtime
{
    public sealed class WaveEntry
    {
        public string EnemyName     { get; }
        public int    Count         { get; }
        public float  SpawnInterval { get; }

        public WaveEntry(string enemyName, int count, float spawnInterval)
        {
            EnemyName     = enemyName;
            Count         = count;
            SpawnInterval = spawnInterval;
        }
    }

    public sealed class WaveData
    {
        public int                      Id              { get; }
        public int                      WaveGroupId     { get; }
        public int                      SequenceIndex   { get; }
        public float                    WaveDuration    { get; }
        public bool                     LoopFromHere    { get; }
        public string                   SpawnActionName { get; }
        public IReadOnlyList<WaveEntry> Entries         { get; }

        public WaveData(int id, int groupId, int seqIdx, float duration,
                        bool loop, string actionName, IReadOnlyList<WaveEntry> entries)
        {
            Id              = id;
            WaveGroupId     = groupId;
            SequenceIndex   = seqIdx;
            WaveDuration    = duration;
            LoopFromHere    = loop;
            SpawnActionName = actionName;
            Entries         = entries;
        }
    }
}
