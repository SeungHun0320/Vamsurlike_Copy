using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data.Runtime;
using Vamsurlike.Network;
using SysRandom = System.Random;

namespace Vamsurlike.Stage
{
    public class WaveController : MonoBehaviour
    {
        [SerializeField] private float spawnRadius = 15f;
        [SerializeField] private int   randomSeed  = 42;

        private SysRandom         rng;
        private EnemySpawnManager spawnManager;
        private int               activeGroupId;
        private WaveData          currentWaveData;

        private readonly Dictionary<string, Func<WaveData, IEnumerator>> spawnActions = new();

        // ─── Init / Begin ───────────────────────────────────────────────
        public void Initialize(EnemySpawnManager enemySpawnManager, int waveGroupId)
        {
            spawnManager  = enemySpawnManager;
            activeGroupId = waveGroupId;
            RegisterSpawnActions();
        }

        public void Begin()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
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
            var sequence = DataManager.GetWaveSequence(activeGroupId);
            if (sequence.Count == 0)
            {
                Debug.LogWarning($"[{nameof(WaveController)}] waveGroupId={activeGroupId}에 해당하는 웨이브가 없습니다.");
                yield break;
            }

            // 일회성 구간 실행
            for (int i = 0; i < sequence.Count; i++)
            {
                currentWaveData = sequence[i];
                if (!sequence[i].LoopFromHere)
                    yield return StartCoroutine(ExecuteWave(sequence[i]));
                else
                    break;
            }

            // 루프 시작점 탐색
            int loopStart = sequence.FindIndex(w => w.LoopFromHere);
            if (loopStart < 0) loopStart = sequence.Count - 1;

            while (true)
            {
                for (int i = loopStart; i < sequence.Count; i++)
                {
                    currentWaveData = sequence[i];
                    yield return StartCoroutine(ExecuteWave(sequence[i]));
                }
            }
        }

        private IEnumerator ExecuteWave(WaveData wave)
        {
            if (!string.IsNullOrEmpty(wave.SpawnActionName))
            {
                if (spawnActions.TryGetValue(wave.SpawnActionName, out var action))
                    yield return StartCoroutine(action(wave));
                else
                {
                    Debug.LogWarning($"[{nameof(WaveController)}] 미등록 spawnActionName='{wave.SpawnActionName}' → 기본 스폰 실행");
                    yield return StartCoroutine(DefaultSpawnWave(wave));
                }
            }
            else
            {
                yield return StartCoroutine(DefaultSpawnWave(wave));
            }

            yield return new WaitForSeconds(wave.WaveDuration);
        }

        // GAME_PLAN §8 Co-op 밸런싱: EnemyHP *= 1+(playerCount-1)*0.3, SpawnRate *= 1+(playerCount-1)*0.5.
        // 모든 스폰 경로(기본/엘리트 링/보스 부하)가 동일한 배율을 쓰도록 한 곳에 모아둔다 —
        // 예전엔 SpawnEliteRing이 이 계산을 누락해 인원수가 늘어도 엘리트만 안 강해지는 버그가 있었다.
        private (float hpMul, float dmgMul, float rateMul) GetCurrentMultipliers()
        {
            float elapsed     = StageRuntime.Instance != null ? StageRuntime.Instance.ElapsedTime.Value : 0f;
            var   scaling     = DataManager.GetScaling(elapsed);
            int   playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;

            float coopHpMul   = Mathf.Max(1f, 1f + (playerCount - 1) * 0.3f);
            float coopRateMul = Mathf.Max(1f, 1f + (playerCount - 1) * 0.5f);

            return (
                scaling.HpMultiplier * coopHpMul,
                scaling.DamageMultiplier,
                scaling.SpawnRateMultiplier * coopRateMul);
        }

        // ─── Default Spawn ──────────────────────────────────────────────
        private IEnumerator DefaultSpawnWave(WaveData wave)
        {
            (float hpMul, float dmgMul, float rateMul) = GetCurrentMultipliers();

            foreach (var entry in wave.Entries)
            {
                int   count    = Mathf.RoundToInt(entry.Count * rateMul);
                float interval = entry.SpawnInterval / rateMul;

                for (int i = 0; i < count; i++)
                {
                    SpawnNearRandomPlayer(entry.EnemyName, hpMul, dmgMul);
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        // ─── Custom Spawn Actions ───────────────────────────────────────
        private IEnumerator SpawnEliteRing(WaveData wave)
        {
            if (wave.Entries.Count == 0) yield break;

            var   entry = wave.Entries[0];
            int   count = Mathf.Max(1, entry.Count);
            (float hpMul, float dmgMul, _) = GetCurrentMultipliers();

            for (int i = 0; i < count; i++)
            {
                float   angle = i * (360f / count) * Mathf.Deg2Rad;
                Vector3 pos   = GetCenterPosition() +
                                new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
                spawnManager.SpawnEnemyByName(entry.EnemyName, pos, hpMul, dmgMul);
            }
        }

        private IEnumerator SpawnBossMinions(WaveData wave)
        {
            yield return StartCoroutine(DefaultSpawnWave(wave));
        }

        // ─── Debug ─────────────────────────────────────────────────────
        public void ForceRespawnWave()
        {
            if (currentWaveData == null) return;
            StartCoroutine(ExecuteWave(currentWaveData));
            Debug.Log($"[{nameof(WaveController)}] 디버그 강제 스폰: groupId={activeGroupId}, seq={currentWaveData.SequenceIndex}");
        }

        // ─── Spawn Helpers ──────────────────────────────────────────────
        private void SpawnNearRandomPlayer(string enemyName, float hpMul = 1f, float dmgMul = 1f)
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

            spawnManager.SpawnEnemyByName(enemyName, pos, hpMul, dmgMul);
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
