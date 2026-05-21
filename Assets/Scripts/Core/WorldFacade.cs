using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Core
{
    public class WorldFacade : MonoBehaviour, IWorldFacade
    {
        [SerializeField] private Transform[] spawnPoints;

        private float           stageStartTime;
        private bool            stageCleared;
        private List<EnemyBase> activeEnemies = new List<EnemyBase>();

        private void Awake()
        {
            stageStartTime = Time.time;
        }

        // ── IWorldFacade ───────────────────────────────────────────────────

        public float GetStageElapsedTime() => Time.time - stageStartTime;

        public bool IsStageCleared() => stageCleared;

        public void OnEnemyDied(EnemyBase enemy)
        {
            activeEnemies.Remove(enemy);
            Debug.Log($"[WorldFacade] Enemy died: {enemy.name}. Remaining: {activeEnemies.Count}");
        }

        public void SpawnEnemy(EnemyDataSO data, Vector3 pos)
        {
            if (data == null || data.prefab == null)
            {
                Debug.LogWarning("[WorldFacade] SpawnEnemy called with null data or prefab.");
                return;
            }
            GameObject go = Instantiate(data.prefab, pos, Quaternion.identity);
            if (go.TryGetComponent(out EnemyBase enemy))
            {
                enemy.Initialize(data);
                activeEnemies.Add(enemy);
            }
        }

        public Vector3 GetRandomSpawnPoint()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return Vector3.zero;
            return spawnPoints[Random.Range(0, spawnPoints.Length)].position;
        }

        // ── Public helpers ─────────────────────────────────────────────────

        public void SetStageCleared(bool value) => stageCleared = value;
    }
}
