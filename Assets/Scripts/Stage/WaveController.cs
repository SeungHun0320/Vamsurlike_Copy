using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;
using SysRandom = System.Random;

namespace Vamsurlike.Stage
{
    public class WaveController : MonoBehaviour
    {
        [SerializeField] private WaveTableSO          waveTable;
        [SerializeField] private EnemyScalingTableSO  scalingTable;
        [SerializeField] private float                spawnRadius = 15f;
        [SerializeField] private int                  randomSeed  = 42;

        private SysRandom          rng;
        private EnemySpawnManager  spawnManager;
        private int                activeGroupId;
        private int                currentSequenceIndex;
        private WaveRow            currentWaveRow;

        private readonly Dictionary<string, Func<WaveRow, IEnumerator>> spawnActions = new();

        // ─── Validation ────────────────────────────────────────────────
        public bool Validate()
        {
            bool valid = true;

            if (waveTable == null)
            {
                Debug.LogError($"[{nameof(WaveController)}] waveTable이 할당되지 않았습니다.", this);
                valid = false;
            }
            if (scalingTable == null)
            {
                Debug.LogError($"[{nameof(WaveController)}] scalingTable이 할당되지 않았습니다.", this);
                valid = false;
            }

            return valid;
        }

        // ─── Init / Begin ───────────────────────────────────────────────
        // StageRuntime.OnNetworkSpawn에서 Initialize → Begin 순으로 호출
        public void Initialize(EnemySpawnManager enemySpawnManager, int waveGroupId)
        {
            spawnManager  = enemySpawnManager;
            activeGroupId = waveGroupId;
            RegisterSpawnActions();
        }

        public void Begin()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (waveTable == null) return;
            rng = new SysRandom(randomSeed);
            StartCoroutine(RunWaves());
        }

        // ─── Named Spawn Action Registry ───────────────────────────────
        private void RegisterSpawnActions()
        {
            spawnActions.Clear();
            spawnActions["SpawnEliteRing"]   = SpawnEliteRing;
            spawnActions["SpawnBossMinions"] = SpawnBossMinions;
        }

        // ─── Wave Sequence ──────────────────────────────────────────────
        private IEnumerator RunWaves()
        {
            var sequence = BuildSequence(activeGroupId);
            if (sequence.Count == 0)
            {
                Debug.LogWarning($"[{nameof(WaveController)}] waveGroupId={activeGroupId}에 해당하는 웨이브가 없습니다.");
                yield break;
            }

            // 일회성 구간 실행
            for (int i = 0; i < sequence.Count; i++)
            {
                currentSequenceIndex = i;
                currentWaveRow       = sequence[i];
                if (!sequence[i].loopFromHere)
                    yield return StartCoroutine(ExecuteWave(sequence[i]));
                else
                    break;
            }

            // 루프 시작점 탐색
            int loopStart = sequence.FindIndex(w => w.loopFromHere);
            if (loopStart < 0) loopStart = sequence.Count - 1;

            while (true)
            {
                for (int i = loopStart; i < sequence.Count; i++)
                {
                    currentSequenceIndex = i;
                    currentWaveRow       = sequence[i];
                    yield return StartCoroutine(ExecuteWave(sequence[i]));
                }
            }
        }

        private List<WaveRow> BuildSequence(int groupId)
        {
            var result = new List<WaveRow>();
            for (int i = 0; i < waveTable.Count; i++)
            {
                var row = waveTable[i];
                if (row.waveGroupId == groupId)
                    result.Add(row);
            }
            result.Sort((a, b) => a.sequenceIndex.CompareTo(b.sequenceIndex));
            return result;
        }

        private IEnumerator ExecuteWave(WaveRow wave)
        {
            if (!string.IsNullOrEmpty(wave.spawnActionName))
            {
                if (spawnActions.TryGetValue(wave.spawnActionName, out var action))
                {
                    yield return StartCoroutine(action(wave));
                }
                else
                {
                    Debug.LogWarning($"[{nameof(WaveController)}] 미등록 spawnActionName='{wave.spawnActionName}' → 기본 스폰 실행");
                    yield return StartCoroutine(DefaultSpawnWave(wave));
                }
            }
            else
            {
                yield return StartCoroutine(DefaultSpawnWave(wave));
            }

            yield return new WaitForSeconds(wave.waveDuration);
        }

        // ─── Default Spawn (entries 기반) ───────────────────────────────
        private IEnumerator DefaultSpawnWave(WaveRow wave)
        {
            if (wave.entries == null) yield break;

            float elapsed = StageRuntime.Instance != null ? StageRuntime.Instance.ElapsedTime.Value : 0f;
            var scaling   = GetCurrentScaling(elapsed);

            int   playerCount  = NetworkManager.Singleton.ConnectedClientsList.Count;
            float hpMul        = scaling.hpMultiplier        * Mathf.Max(1f, 1f + (playerCount - 1) * 0.3f);
            float dmgMul       = scaling.damageMultiplier;
            float rateMul      = scaling.spawnRateMultiplier * Mathf.Max(1f, 1f + (playerCount - 1) * 0.5f);

            foreach (var entry in wave.entries)
            {
                if (entry == null || entry.enemyData == null) continue;

                int   count    = Mathf.RoundToInt(entry.count * rateMul);
                float interval = entry.spawnInterval / rateMul;

                for (int i = 0; i < count; i++)
                {
                    SpawnNearRandomPlayer(entry.enemyData, hpMul, dmgMul);
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        // ─── Custom Spawn Actions ───────────────────────────────────────
        // 원형 포위 — 8방향에서 엘리트 동시 스폰
        private IEnumerator SpawnEliteRing(WaveRow wave)
        {
            if (wave.entries == null || wave.entries.Length == 0) yield break;

            var   entry    = wave.entries[0];
            if (entry == null || entry.enemyData == null) yield break;

            float elapsed  = StageRuntime.Instance != null ? StageRuntime.Instance.ElapsedTime.Value : 0f;
            var   scaling  = GetCurrentScaling(elapsed);
            int   count    = Mathf.Max(1, entry.count);

            for (int i = 0; i < count; i++)
            {
                float   angle = i * (360f / count) * Mathf.Deg2Rad;
                Vector3 pos   = GetCenterPosition() +
                                new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
                spawnManager.SpawnEnemy(entry.enemyData, pos, scaling.hpMultiplier, scaling.damageMultiplier);
            }

            yield break;
        }

        // 보스 미니언 — 보스 처치 전까지 추가 스폰. 현재는 기본 스폰으로 위임
        private IEnumerator SpawnBossMinions(WaveRow wave)
        {
            yield return StartCoroutine(DefaultSpawnWave(wave));
        }

        // ─── Debug ─────────────────────────────────────────────────────
        public void ForceRespawnWave()
        {
            StartCoroutine(ExecuteWave(currentWaveRow));
            Debug.Log($"[{nameof(WaveController)}] 디버그 강제 스폰: groupId={activeGroupId}, seq={currentSequenceIndex}");
        }

        // ─── Scaling Lookup ─────────────────────────────────────────────
        private ScalingRow GetCurrentScaling(float elapsedSeconds)
        {
            if (scalingTable == null || scalingTable.Count == 0)
                return new ScalingRow { hpMultiplier = 1f, damageMultiplier = 1f, spawnRateMultiplier = 1f };

            float     elapsedMinutes = elapsedSeconds / 60f;
            ScalingRow result        = scalingTable[0];

            for (int i = 1; i < scalingTable.Count; i++)
            {
                if (scalingTable[i].timeMinutes <= elapsedMinutes)
                    result = scalingTable[i];
                else
                    break;
            }
            return result;
        }

        // ─── Spawn Helpers ──────────────────────────────────────────────
        private void SpawnNearRandomPlayer(EnemyDataSO data, float hpMul = 1f, float dmgMul = 1f)
        {
            if (spawnManager == null) return;

            var clients = NetworkManager.Singleton.ConnectedClientsList;
            if (clients.Count == 0) return;

            var     target = clients[rng.Next(clients.Count)];
            Vector3 center = target.PlayerObject != null
                ? target.PlayerObject.transform.position
                : Vector3.zero;

            double  angle = rng.NextDouble() * System.Math.PI * 2.0;
            var     dir   = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));
            Vector3 pos   = center + new Vector3(dir.x, 0f, dir.y) * spawnRadius;

            spawnManager.SpawnEnemy(data, pos, hpMul, dmgMul);
        }

        private Vector3 GetCenterPosition()
        {
            var clients = NetworkManager.Singleton.ConnectedClientsList;
            if (clients.Count == 0) return Vector3.zero;

            Vector3 sum = Vector3.zero;
            int     cnt = 0;
            foreach (var c in clients)
            {
                if (c.PlayerObject == null) continue;
                sum += c.PlayerObject.transform.position;
                cnt++;
            }
            return cnt > 0 ? sum / cnt : Vector3.zero;
        }
    }
}
