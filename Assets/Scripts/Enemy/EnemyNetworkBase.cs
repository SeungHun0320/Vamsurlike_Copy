using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Stage;

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

        // 서버가 스폰 후 데이터 주입 시 사용
        public void Initialize(EnemyDataSO enemyData)
        {
            if (!IsServer) return;
            data = enemyData;
            HP.Value = data.hp;
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

            if (data != null && data.xpDrop > 0)
                XPOrbManager.Instance?.SpawnOrb(transform.position, data.xpDrop);

            NetworkObject.Despawn(true);
        }

        [ClientRpc]
        private void PlayDeathVFXClientRpc()
        {
            // Phase 4에서 VFX 연결
        }
    }
}
