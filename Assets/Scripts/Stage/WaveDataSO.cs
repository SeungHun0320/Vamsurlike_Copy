using System;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Stage
{
    [Serializable]
    public class WaveEntryData
    {
        public EnemyDataSO enemyData;
        [Min(1)] public int count = 5;
        [Min(0.05f)] public float spawnInterval = 0.5f;
    }

    [CreateAssetMenu(menuName = "Vamsurlike/WaveData", fileName = "WaveData")]
    public class WaveDataSO : ScriptableObject
    {
        [Tooltip("이 웨이브가 끝난 뒤 다음 웨이브 시작까지 대기(초)")]
        [Min(1f)] public float waveDuration = 30f;
        [Tooltip("count 수치의 기준 플레이어 수. 초과 시 플레이어당 50% 추가 스폰")]
        [Min(1)] public int basePlayerCount = 1;
        public WaveEntryData[] entries;
    }
}
