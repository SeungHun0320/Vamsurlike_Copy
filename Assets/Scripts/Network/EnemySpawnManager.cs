using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Vamsurlike.Data;
using Vamsurlike.Enemy;


namespace Vamsurlike.Network
{
    public class EnemySpawnManager : MonoBehaviour
    {
        public static EnemySpawnManager Instance { get; private set; }

        // Inspector에서 모든 EnemyDataSO를 등록. 이름 기반 스폰에 사용됨.
        [SerializeField] private List<EnemyDataSO> enemyRegistry = new();

        private readonly Dictionary<string, EnemyDataSO> enemyByName = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;

            foreach (var data in enemyRegistry)
                if (data != null) enemyByName[data.name] = data;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SpawnEnemy(EnemyDataSO data, Vector3 position, float hpMultiplier = 1f, float damageMultiplier = 1f)
        {
            if (!IsServerActive()) return;
            if (data?.prefab == null)
            {
                Debug.LogError($"[{nameof(EnemySpawnManager)}] EnemyDataSO.prefab이 설정되지 않았습니다.");
                return;
            }

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                position = hit.position;

            NetworkObject networkObject = PoolManager.GetOrInstantiateNetworkObject(
                data.prefab, position, Quaternion.identity, nameof(EnemySpawnManager));
            if (networkObject == null) return;

            networkObject.Spawn(true);
            if (networkObject.TryGetComponent<EnemyNetworkBase>(out var enemyBase))
                enemyBase.Initialize(data, hpMultiplier, damageMultiplier);
        }

        // 이름으로 적 스폰 — DataManager 기반 웨이브에서 사용
        public void SpawnEnemyByName(string enemyName, Vector3 position, float hpMul = 1f, float dmgMul = 1f)
        {
            if (!enemyByName.TryGetValue(enemyName, out var data))
            {
                Debug.LogError($"[{nameof(EnemySpawnManager)}] '{enemyName}' 미등록. Inspector의 enemyRegistry를 확인하세요.");
                return;
            }
            SpawnEnemy(data, position, hpMul, dmgMul);
        }

        public void SpawnBossByName(string enemyName)
        {
            if (!enemyByName.TryGetValue(enemyName, out var data))
            {
                Debug.LogError($"[{nameof(EnemySpawnManager)}] 보스 '{enemyName}' 미등록.");
                return;
            }
            SpawnBoss(data);
        }

        // 보스 스폰: NavMesh 위 맵 중앙 또는 플레이어들의 평균 위치 근처에 배치
        public void SpawnBoss(EnemyDataSO data)
        {
            if (!IsServerActive()) return;
            if (data?.prefab == null)
            {
                Debug.LogError($"[{nameof(EnemySpawnManager)}] SpawnBoss: EnemyDataSO.prefab이 설정되지 않았습니다.");
                return;
            }

            Vector3 spawnPos = GetBossSpawnPosition();
            SpawnEnemy(data, spawnPos, hpMultiplier: 1f, damageMultiplier: 1f);
        }

        private Vector3 GetBossSpawnPosition()
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
            Vector3 center = cnt > 0 ? sum / cnt : Vector3.zero;

            // 플레이어 평균 위치에서 20m 전방에 보스 스폰
            return center + Vector3.forward * 20f;
        }

        private static bool IsServerActive() =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }
}
