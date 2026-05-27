using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;
using UnityEngine.InputSystem;

namespace Vamsurlike.Stage
{
    // 임시 스폰 테스터 — WaveController 완성 후 제거
    public class SpawnTester : MonoBehaviour
    {
        [SerializeField] private EnemyDataSO enemyData;
        [SerializeField] private float spawnRadius = 8f;
        [SerializeField] private KeyCode spawnKey = KeyCode.F5;
        [SerializeField] private KeyCode spawnBurstKey = KeyCode.F6;
        [SerializeField] private int burstCount = 5;

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

        [ContextMenu("Spawn One")]
        private void EditorSpawnOne() => SpawnOne();
    }
}
