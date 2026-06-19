using UnityEngine;

namespace Vamsurlike.Stage
{
    internal static class DebugWaveCommands
    {
        public static bool RespawnWave(StageRuntime runtime)
        {
            if (runtime == null || runtime.Wave == null)
            {
                Debug.LogWarning($"[{nameof(DebugWaveCommands)}] WaveController is missing.");
                return false;
            }

            runtime.Wave.ForceRespawnWave();
            Debug.Log($"[{nameof(DebugWaveCommands)}] Forced wave respawn.");
            return true;
        }
    }
}
