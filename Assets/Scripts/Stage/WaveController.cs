using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;

namespace Vamsurlike.Stage
{
    public class WaveController : MonoBehaviour
    {
        [SerializeField] private WaveDataSO[] waves;
        [SerializeField] private float spawnRadius = 15f;
        [SerializeField] private bool loopLastWave = true;

        private void Start()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                enabled = false;
                return;
            }
            if (waves == null || waves.Length == 0) return;
            StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves()
        {
            for (int i = 0; i < waves.Length; i++)
            {
                yield return StartCoroutine(SpawnWave(waves[i]));
                yield return new WaitForSeconds(waves[i].waveDuration);
            }

            if (!loopLastWave || waves.Length == 0) yield break;

            var last = waves[waves.Length - 1];
            while (true)
            {
                yield return StartCoroutine(SpawnWave(last));
                yield return new WaitForSeconds(last.waveDuration);
            }
        }

        private IEnumerator SpawnWave(WaveDataSO wave)
        {
            int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
            float multiplier = Mathf.Max(1f,
                1f + (playerCount - wave.basePlayerCount) * 0.5f);

            foreach (var entry in wave.entries)
            {
                if (entry.enemyData == null) continue;
                int count = Mathf.RoundToInt(entry.count * multiplier);
                for (int i = 0; i < count; i++)
                {
                    SpawnNearRandomPlayer(entry.enemyData);
                    yield return new WaitForSeconds(entry.spawnInterval);
                }
            }
        }

        private void SpawnNearRandomPlayer(EnemyDataSO data)
        {
            if (EnemySpawnManager.Instance == null) return;

            var clients = NetworkManager.Singleton.ConnectedClientsList;
            if (clients.Count == 0) return;

            var target = clients[Random.Range(0, clients.Count)];
            Vector3 center = target.PlayerObject != null
                ? target.PlayerObject.transform.position
                : Vector3.zero;

            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector3 pos = center + new Vector3(dir.x, 0f, dir.y) * spawnRadius;
            EnemySpawnManager.Instance.SpawnEnemy(data, pos);
        }
    }
}
