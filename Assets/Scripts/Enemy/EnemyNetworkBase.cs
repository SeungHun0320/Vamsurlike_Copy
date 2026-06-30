using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;
using Vamsurlike.Player;
using Vamsurlike.Skills;
using Vamsurlike.Stage;
using Vamsurlike.UI;

namespace Vamsurlike.Enemy
{
    public class EnemyNetworkBase : NetworkBehaviour
    {
        private static readonly Color BossTelegraphColor = new(1f, 0.1f, 0.05f, 0.9f);

        [SerializeField] private EnemyDataSO data;

        public readonly NetworkVariable<float> HP = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> MaxHPValue = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsAlive => HP.Value > 0f;
        public EnemyDataSO Data => data;
        public float MaxHP => MaxHPValue.Value;
        public float ScaledAttackPower { get; private set; }

        private const ulong NoAttacker = ulong.MaxValue;
        private ulong lastAttackerClientId = NoAttacker;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            MaxHPValue.Value = data != null ? data.hp : 100f;
            HP.Value = MaxHPValue.Value;
            EnemyRegistry.Register(this);
        }

        // EnemySpawnManager.SpawnEnemy에서 Spawn() 직후 호출
        public void Initialize(EnemyDataSO enemyData, float hpMultiplier = 1f, float damageMultiplier = 1f)
        {
            if (!IsServer) return;
            data = enemyData;
            MaxHPValue.Value = Mathf.Max(1f, data.hp * Mathf.Max(1f, hpMultiplier));
            HP.Value          = MaxHPValue.Value;
            ScaledAttackPower = data.attackPower * Mathf.Max(1f, damageMultiplier);
            // OnNetworkSpawn보다 뒤에 호출되므로 EnemyAI에 데이터를 직접 주입
            if (TryGetComponent<EnemyAI>(out var ai))
            {
                ai.ApplyData(data);

                if (data.isBoss)
                {
                    var patterns = GetComponent<BossPatternController>();
                    if (patterns == null) patterns = gameObject.AddComponent<BossPatternController>();
                    patterns.Configure(this, ai);
                }
                else if (TryGetComponent<BossPatternController>(out var patterns))
                {
                    patterns.StopPatterns();
                    patterns.enabled = false;
                }
            }
        }

        // attackerClientId: 플레이어 귀속 시 OwnerClientId, 환경/디버그 비귀속 시 ulong.MaxValue
        // skillTag: 스킬/아이템 식별자 (SkillDataSO.name). 비귀속 시 null
        public void TakeDamage(float amount, ulong attackerClientId = NoAttacker, string skillTag = null)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[{nameof(EnemyNetworkBase)}] TakeDamage ignored on client. enemy={name}, amount={amount}");
                return;
            }

            if (!IsAlive) return;

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

            HP.Value = Mathf.Max(0f, HP.Value - finalDamage);

            if (attackerClientId != NoAttacker)
            {
                lastAttackerClientId = attackerClientId;
                GetPlayerMatchStats(attackerClientId)?.AddDamage(finalDamage, skillTag);
                GetSkillManager(attackerClientId)?.AddUltimateGaugeForDamage(finalDamage);
            }

            float offset = data != null ? data.floatingTextHeightOffset : 2f;
            ShowDamageClientRpc(finalDamage, transform.position + Vector3.up * offset);

            if (HP.Value <= 0f)
                HandleDeath();
        }

        private PlayerMatchStats GetPlayerMatchStats(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return null;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            return client.PlayerObject?.GetComponent<PlayerMatchStats>();
        }

        private SkillManager GetSkillManager(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return null;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            if (client.PlayerObject == null) return null;
            return client.PlayerObject.GetComponent<SkillManager>();
        }

        // 서버가 지정한 보스 광역기 범위를 Damage Aura와 같은 원형 VFX로 전 클라이언트에 표시.
        public void ShowSlamTelegraph(Vector3 center, float radius, float duration)
        {
            if (!IsServer) return;
            ShowSlamTelegraphClientRpc(center, radius, duration);
        }

        // 모르타르 패턴: 여러 위치에 경고 원을 동시에 표시.
        public void ShowMortarTelegraph(Vector3[] positions, float radius, float duration)
        {
            if (!IsServer) return;
            ShowMortarTelegraphClientRpc(positions, radius, duration);
        }

        protected virtual void HandleDeath()
        {
            bool wasBoss = data != null && data.isBoss;

            if (lastAttackerClientId != NoAttacker)
            {
                PlayerMatchStats matchStats = GetPlayerMatchStats(lastAttackerClientId);
                if (matchStats != null) matchStats.AddKill();

                SkillManager skillManager = GetSkillManager(lastAttackerClientId);
                if (skillManager != null) skillManager.AddUltimateGaugeForKill();
            }

            TriggerDeathAnimClientRpc();
            PlayDeathVFXClientRpc();
            if (StageRuntime.Instance != null && StageRuntime.Instance.Drops != null)
                StageRuntime.Instance.Drops.OnEnemyDied(data, transform.position);

            if (TryGetComponent<BossPatternController>(out var patterns))
            {
                patterns.StopPatterns();
                patterns.enabled = false;
            }

            if (wasBoss && GameFlowCoordinator.Instance != null && GameFlowCoordinator.Instance.IsBossPhase)
                GameFlowCoordinator.Instance.ForceTransition(GameFlowState.Clear);

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
        private void ShowSlamTelegraphClientRpc(Vector3 center, float radius, float duration)
        {
            var visual = new GameObject("BossSlamTelegraph");
            visual.transform.position = center + Vector3.up * 0.05f;

            AreaCircleVFX circle = visual.AddComponent<AreaCircleVFX>();
            circle.Initialize(radius, duration, BossTelegraphColor);
        }

        [ClientRpc]
        private void ShowMortarTelegraphClientRpc(Vector3[] positions, float radius, float duration)
        {
            foreach (var pos in positions)
            {
                var visual = new GameObject("BossMortarTelegraph");
                visual.transform.position = pos + Vector3.up * 0.05f;

                AreaCircleVFX circle = visual.AddComponent<AreaCircleVFX>();
                circle.Initialize(radius, duration, BossTelegraphColor);
            }
        }

        [ClientRpc]
        private void TriggerDeathAnimClientRpc()
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim != null) anim.SetTrigger(Animator.StringToHash("Die"));
        }

        [ClientRpc]
        private void PlayDeathVFXClientRpc()
        {
            // Phase 8에서 VFX 연결
        }

    }
}
