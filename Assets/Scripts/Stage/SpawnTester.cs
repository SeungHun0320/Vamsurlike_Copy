using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Network;
using UnityEngine.InputSystem;

namespace Vamsurlike.Stage
{
    // 임시 스폰 테스터 — WaveController 완성 후 제거
    // F5: 적 1마리 스폰 | F6: 버스트 스폰 | F7: 전체 적 데미지 | F8: 전체 적 즉사
    public class SpawnTester : MonoBehaviour
    {
        [SerializeField] private EnemyDataSO enemyData;
        [SerializeField] private float spawnRadius = 8f;
        [SerializeField] private int burstCount = 5;
        [SerializeField] private float debugDamage = 20f;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
                Debug.Log($"[SpawnTester] F5 — IsServer={isServer}, EnemySpawnManager={EnemySpawnManager.Instance != null}");
                SpawnOne();
            }

            if (keyboard.f6Key.wasPressedThisFrame)
            {
                bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
                Debug.Log($"[SpawnTester] F6 burst({burstCount}) — IsServer={isServer}");
                for (int i = 0; i < burstCount; i++)
                    SpawnOne();
            }

            if (keyboard.f7Key.wasPressedThisFrame)
                DamageAllEnemies(debugDamage);

            if (keyboard.f8Key.wasPressedThisFrame)
                DamageAllEnemies(float.MaxValue);
        }

        private void SpawnOne()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[SpawnTester] 서버에서만 스폰 가능합니다.");
                return;
            }
            if (enemyData == null)
            {
                Debug.LogWarning("[SpawnTester] enemyData가 설정되지 않았습니다.");
                return;
            }
            if (EnemySpawnManager.Instance == null)
            {
                Debug.LogWarning("[SpawnTester] EnemySpawnManager.Instance가 null입니다. 씬에 배치됐는지 확인하세요.");
                return;
            }

            Vector2 circle = Random.insideUnitCircle.normalized * spawnRadius;
            Vector3 pos = transform.position + new Vector3(circle.x, 0f, circle.y);
            EnemySpawnManager.Instance.SpawnEnemy(enemyData, pos);
        }

        private void DamageAllEnemies(float amount)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[SpawnTester] 서버에서만 데미지 가능합니다.");
                return;
            }
            var enemies = FindObjectsByType<EnemyNetworkBase>(FindObjectsSortMode.None);
            foreach (var e in enemies)
                e.TakeDamage(amount);
            Debug.Log($"[SpawnTester] 데미지 {amount} → {enemies.Length}마리");
        }

        [ContextMenu("Spawn One")]
        private void EditorSpawnOne() => SpawnOne();
    }
}
