using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;
using Vamsurlike.Stage; // DropManager

namespace Vamsurlike.Enemy
{
    public class EnemyNetworkBase : NetworkBehaviour
    {
        [SerializeField] private EnemyDataSO data;

        public readonly NetworkVariable<float> HP = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsAlive => HP.Value > 0f;
        public EnemyDataSO Data => data;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                HP.Value = data != null ? data.hp : 100f;
        }

        // EnemySpawnManager.SpawnEnemy에서 Spawn() 직후 호출
        public void Initialize(EnemyDataSO enemyData)
        {
            if (!IsServer) return;
            data = enemyData;
            HP.Value = data.hp;
            // OnNetworkSpawn보다 뒤에 호출되므로 EnemyAI에 데이터를 직접 주입
            if (TryGetComponent<EnemyAI>(out var ai))
                ai.ApplyData(data);
        }

        public void TakeDamage(float amount)
        {
            if (!IsServer || !IsAlive || amount <= 0f) return;
            HP.Value = Mathf.Max(0f, HP.Value - amount);
            if (HP.Value <= 0f)
                HandleDeath();
        }

        protected virtual void HandleDeath()
        {
            PlayDeathVFXClientRpc();
            DropManager.Instance?.OnEnemyDied(data, transform.position);
            NetworkObject.Despawn(false);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            // 서버: Despawn(false) 시 PrefabHandler.Destroy 미호출 → 여기서 반환
            // 클라이언트: PrefabHandler.Destroy가 반환 담당 — 중복 push 방지
            if (!IsServer) return;
            if (data != null && data.prefab != null && PoolManager.Instance != null)
                PoolManager.Instance.ReturnNetworkObject(data.prefab, NetworkObject);
        }

        [ClientRpc]
        private void PlayDeathVFXClientRpc()
        {
            // Phase 4에서 VFX 연결
        }
    }
}
