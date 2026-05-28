using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;
using SysRandom = System.Random;

namespace Vamsurlike.Stage
{
    public class WaveController : MonoBehaviour
    {
        [SerializeField] private WaveDataSO[] waves;
        [SerializeField] private float spawnRadius = 15f;
        [SerializeField] private bool loopLastWave = true;
        [SerializeField] private int randomSeed = 42;

        private SysRandom rng;
        private EnemySpawnManager spawnManager;

        // StageRuntime.OnNetworkSpawn에서 Initialize → Begin 순으로 호출
        public void Initialize(EnemySpawnManager enemySpawnManager)
        {
            spawnManager = enemySpawnManager;
        }

        public void Begin()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (waves == null || waves.Length == 0) return;
            rng = new SysRandom(randomSeed);
            StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves()
        {
            for (int i = 0; i < waves.Length; i++)
            {
                WaveDataSO wave = waves[i];
                if (wave == null) continue;
                yield return StartCoroutine(SpawnWave(wave));
                yield return new WaitForSeconds(wave.waveDuration);
            }

            if (!loopLastWave || waves.Length == 0) yield break;

            var last = waves[waves.Length - 1];
            if(last == null)
                yield break;

            while (true)
            {
                yield return StartCoroutine(SpawnWave(last));
                yield return new WaitForSeconds(last.waveDuration);
            }
        }

        private IEnumerator SpawnWave(WaveDataSO wave)
        {
            if (wave == null || wave.entries == null) yield break;

            int   playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
            float t           = StageRuntime.Instance != null ? StageRuntime.Instance.ElapsedTime : 0f;
            float tMin        = t / 60f;

            // GAME_PLAN §8 시간 기반 난이도
            float hpTimeMul   = 1f + tMin * 0.15f;
            float dmgTimeMul  = 1f + tMin * 0.10f;
            float rateTimeMul = 1f + tMin * 0.20f;

            // GAME_PLAN §8 Co-op 배율
            float hpPlayerMul   = Mathf.Max(1f, 1f + (playerCount - 1) * 0.3f);
            float ratePlayerMul = Mathf.Max(1f, 1f + (playerCount - wave.basePlayerCount) * 0.5f);

            float hpMultiplier        = hpPlayerMul * hpTimeMul;
            float damageMultiplier    = dmgTimeMul;
            float spawnRateMultiplier = ratePlayerMul * rateTimeMul;

            foreach (var entry in wave.entries)
            {
                if (entry.enemyData == null) continue;
                int   count          = Mathf.RoundToInt(entry.count * spawnRateMultiplier);
                float scaledInterval = entry.spawnInterval / spawnRateMultiplier;

                for (int i = 0; i < count; i++)
                {
                    SpawnNearRandomPlayer(entry.enemyData, hpMultiplier, damageMultiplier);
                    yield return new WaitForSeconds(scaledInterval);
                }
            }
        }

        private void SpawnNearRandomPlayer(EnemyDataSO data, float hpMultiplier = 1f, float damageMultiplier = 1f)
        {
            if (spawnManager == null) return;

            var clients = NetworkManager.Singleton.ConnectedClientsList;
            if (clients.Count == 0) return;

            var target = clients[rng.Next(clients.Count)];
            Vector3 center = target.PlayerObject != null
                ? target.PlayerObject.transform.position
                : Vector3.zero;

            double angle = rng.NextDouble() * System.Math.PI * 2.0;
            var dir = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));
            Vector3 pos = center + new Vector3(dir.x, 0f, dir.y) * spawnRadius;
            spawnManager.SpawnEnemy(data, pos, hpMultiplier, damageMultiplier);
        }
    }
}
