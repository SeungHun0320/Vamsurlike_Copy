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

        public void SpawnEnemy(EnemyDataSO data, Vector3 position)
        {
            if (!IsServerActive()) return;
            if (data?.prefab == null)
            {
                Debug.LogError($"[{nameof(EnemySpawnManager)}] EnemyDataSO.prefab이 설정되지 않았습니다.");
                return;
            }

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                position = hit.position;

            var instance = Instantiate(data.prefab, position, Quaternion.identity);
            if (!instance.TryGetComponent<NetworkObject>(out var networkObject))
            {
                Debug.LogError($"[{nameof(EnemySpawnManager)}] {data.enemyName} prefab에 NetworkObject가 없습니다.", instance);
                Destroy(instance);
                return;
            }

            networkObject.Spawn(true);
            instance.GetComponent<EnemyNetworkBase>()?.Initialize(data);
            Debug.Log($"[{nameof(EnemySpawnManager)}] {data.enemyName} 스폰 완료. position={position}");
        }

        private static bool IsServerActive() =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }
}
