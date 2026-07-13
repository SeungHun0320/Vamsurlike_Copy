using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Vamsurlike.Data.Runtime;
using Vamsurlike.Network;
using SysRandom = System.Random;

namespace Vamsurlike.Stage
{
    public class WaveController : MonoBehaviour
    {
        [SerializeField] private float spawnRadius = 15f; // SpawnEliteRing 전용 — 플레이어 그룹을 포위하는 반경
        [SerializeField] private int   randomSeed  = 42;

        // 스테이지 Ground에 부착된 MapSpawnBounds가 있으면 그쪽이 우선한다 — 스테이지마다 크기가
        // 다를 수 있으므로 여기 값은 MapSpawnBounds가 없을 때만 쓰이는 폴백(Stage_01 기준)이다.
        [SerializeField] private MapSpawnBounds spawnBounds;
        [SerializeField] private float mapSpawnHalfExtent   = 220f;
        [SerializeField] private float navMeshSampleRadius  = 10f;
        private const int MaxMapSpawnAttempts = 5;

        private float EffectiveHalfExtent   => spawnBounds != null ? spawnBounds.halfExtent          : mapSpawnHalfExtent;
        private float EffectiveSampleRadius => spawnBounds != null ? spawnBounds.navMeshSampleRadius : navMeshSampleRadius;

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
                StartCoroutine(SpawnEntry(entry, hpMul, dmgMul, rateMul));

            yield return new WaitForSeconds(wave.WaveDuration);
        }

        private IEnumerator SpawnEntry(WaveEntry entry, float hpMul, float dmgMul, float rateMul)
        {
            int   count    = Mathf.RoundToInt(entry.Count * rateMul);
            float interval = entry.SpawnInterval / rateMul;

            for (int i = 0; i < count; i++)
            {
                SpawnAnywhereOnMap(entry.EnemyName, hpMul, dmgMul);
                if (interval > 0f)
                    yield return new WaitForSeconds(interval);
            }
        }

        // ─── Custom Spawn Actions ───────────────────────────────────────
        private IEnumerator SpawnEliteRing(WaveData wave)
        {
            if (wave.Entries.Count == 0) yield break;

            var   entry = wave.Entries[0];
            int   count = Mathf.Max(1, entry.Count);
            (float hpMul, float dmgMul, float rateMul) = GetCurrentMultipliers();

            for (int i = 0; i < count; i++)
            {
                float   angle = i * (360f / count) * Mathf.Deg2Rad;
                Vector3 pos   = GetCenterPosition() +
                                new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
                spawnManager.SpawnEnemyByName(entry.EnemyName, pos, hpMul, dmgMul);
            }

            for (int i = 1; i < wave.Entries.Count; i++)
                StartCoroutine(SpawnEntry(wave.Entries[i], hpMul, dmgMul, rateMul));

            yield return new WaitForSeconds(wave.WaveDuration);
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
        // 기본 웨이브 스폰 — 플레이어 근처로만 몰리지 않도록 맵 전체 범위에서 무작위 지점을 고른다.
        private void SpawnAnywhereOnMap(string enemyName, float hpMul = 1f, float dmgMul = 1f)
        {
            if (spawnManager == null) return;

            Vector3 pos = FindValidMapPosition();
            spawnManager.SpawnEnemyByName(enemyName, pos, hpMul, dmgMul);
        }

        // 맵 범위 내 무작위 좌표를 몇 번 시도해 NavMesh 위 유효한 지점을 찾는다.
        // 전부 실패하면(맵 가장자리 등 NavMesh 없는 지점만 뽑힌 경우) 원점 근처로 안전하게 폴백한다.
        private Vector3 FindValidMapPosition()
        {
            float halfExtent   = EffectiveHalfExtent;
            float sampleRadius = EffectiveSampleRadius;

            for (int attempt = 0; attempt < MaxMapSpawnAttempts; attempt++)
            {
                float x = (float)(rng.NextDouble() * 2.0 - 1.0) * halfExtent;
                float z = (float)(rng.NextDouble() * 2.0 - 1.0) * halfExtent;

                if (NavMesh.SamplePosition(new Vector3(x, 0f, z), out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                    return hit.position;
            }

            Debug.LogWarning($"[{nameof(WaveController)}] 맵 전체 스폰 위치를 {MaxMapSpawnAttempts}회 시도했지만 NavMesh를 찾지 못해 원점 근처로 폴백합니다.");
            return Vector3.zero;
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
