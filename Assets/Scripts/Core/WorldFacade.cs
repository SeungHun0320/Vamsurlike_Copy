using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Core
{
    public class WorldFacade : MonoBehaviour, IWorldFacade
    {
        [SerializeField] private Transform[] m_spawnPoints;

        private float               m_fStageStartTime;
        private bool                m_bStageCleared;
        private List<EnemyBase>     m_activeEnemies = new List<EnemyBase>();

        private void Awake()
        {
            m_fStageStartTime = Time.time;
        }

        // ── IWorldFacade ───────────────────────────────────────────────────

        public float GetStageElapsedTime() => Time.time - m_fStageStartTime;

        public bool IsStageCleared() => m_bStageCleared;

        public void OnEnemyDied(EnemyBase enemy)
        {
            m_activeEnemies.Remove(enemy);
            Debug.Log($"[WorldFacade] Enemy died: {enemy.name}. Remaining: {m_activeEnemies.Count}");
        }

        public void SpawnEnemy(EnemyDataSO data, Vector3 pos)
        {
            if (data == null || data.m_goPrefab == null)
            {
                Debug.LogWarning("[WorldFacade] SpawnEnemy called with null data or prefab.");
                return;
            }
            GameObject go = Instantiate(data.m_goPrefab, pos, Quaternion.identity);
            if (go.TryGetComponent(out EnemyBase enemy))
            {
                enemy.Initialize(data);
                m_activeEnemies.Add(enemy);
            }
        }

        public Vector3 GetRandomSpawnPoint()
        {
            if (m_spawnPoints == null || m_spawnPoints.Length == 0)
                return Vector3.zero;
            return m_spawnPoints[Random.Range(0, m_spawnPoints.Length)].position;
        }

        // ── Public helpers ─────────────────────────────────────────────────

        public void SetStageCleared(bool value) => m_bStageCleared = value;
    }
}
