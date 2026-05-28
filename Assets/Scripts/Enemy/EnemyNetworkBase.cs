using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;
using Vamsurlike.Stage;
using Vamsurlike.UI;

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
        public float ScaledAttackPower { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            HP.Value = data != null ? data.hp : 100f;
            EnemyRegistry.Register(this);
        }

        // EnemySpawnManager.SpawnEnemy에서 Spawn() 직후 호출
        public void Initialize(EnemyDataSO enemyData, float hpMultiplier = 1f, float damageMultiplier = 1f)
        {
            if (!IsServer) return;
            data = enemyData;
            HP.Value          = Mathf.Max(1f, data.hp * Mathf.Max(1f, hpMultiplier));
            ScaledAttackPower = data.attackPower * Mathf.Max(1f, damageMultiplier);
            // OnNetworkSpawn보다 뒤에 호출되므로 EnemyAI에 데이터를 직접 주입
            if (TryGetComponent<EnemyAI>(out var ai))
                ai.ApplyData(data);
        }

        public void TakeDamage(float amount)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[{nameof(EnemyNetworkBase)}] TakeDamage ignored on client. enemy={name}, amount={amount}");
                return;
            }

            if (!IsAlive)
            {
                Debug.Log($"[{nameof(EnemyNetworkBase)}] TakeDamage ignored because enemy is dead. enemy={name}, amount={amount}, hp={HP.Value}");
                return;
            }

            if (amount <= 0f)
            {
                Debug.LogWarning($"[{nameof(EnemyNetworkBase)}] TakeDamage ignored because amount is invalid. enemy={name}, amount={amount}, hp={HP.Value}");
                return;
            }

            // GAME_PLAN 공식: FinalDamage = amount * (1 + attackMul) * (1 - defenseRate)
            // attackMul은 Phase 5 PassiveStatHandler 구현 전까지 0
            float defense     = Mathf.Max(0f, data != null ? data.defense : 0f);
            float defenseRate = defense / (defense + 100f);
            float finalDamage = Mathf.Max(1f, amount * (1f - defenseRate));

            float beforeHP = HP.Value;
            HP.Value = Mathf.Max(0f, HP.Value - finalDamage);
            Debug.Log($"[{nameof(EnemyNetworkBase)}] {name} TakeDamage raw={amount} final={finalDamage:F1} def={defense}: {beforeHP} -> {HP.Value}");

            float offset = data != null ? data.floatingTextHeightOffset : 2f;
            ShowDamageClientRpc(finalDamage, transform.position + Vector3.up * offset);

            if (HP.Value <= 0f)
                HandleDeath();
        }

        protected virtual void HandleDeath()
        {
            PlayDeathVFXClientRpc();
            if (StageRuntime.Instance != null && StageRuntime.Instance.Drops != null)
                StageRuntime.Instance.Drops.OnEnemyDied(data, transform.position);
            NetworkObject.Despawn(false);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (!IsServer) return;
            EnemyRegistry.Unregister(this);
            if (data != null && data.prefab != null && PoolManager.Instance != null)
                PoolManager.Instance.ReturnNetworkObject(data.prefab, NetworkObject);
        }

        [ClientRpc]
        private void ShowDamageClientRpc(float damage, Vector3 worldPosition)
        {
            FloatingTextManager.Instance?.ShowDamage(damage, worldPosition);
        }

        [ClientRpc]
        private void PlayDeathVFXClientRpc()
        {
            // Phase 8에서 VFX 연결
        }
    }
}
