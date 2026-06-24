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

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
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

            NetworkObject networkObject;
            if (PoolManager.Instance != null)
            {
                networkObject = PoolManager.Instance.GetNetworkObject(data.prefab, position, Quaternion.identity);
            }
            else
            {
                var go = Instantiate(data.prefab, position, Quaternion.identity);
                if (!go.TryGetComponent(out networkObject))
                {
                    Debug.LogError($"[{nameof(EnemySpawnManager)}] {data.enemyName} prefab에 NetworkObject가 없습니다.", go);
                    Destroy(go);
                    return;
                }
            }

            if (networkObject == null) return;

            networkObject.Spawn(true);
            if (networkObject.TryGetComponent<EnemyNetworkBase>(out var enemyBase))
                enemyBase.Initialize(data, hpMultiplier, damageMultiplier);
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
