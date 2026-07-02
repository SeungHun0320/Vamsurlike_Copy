using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;
using Vamsurlike.Player;
using Vamsurlike.Skills;
using Vamsurlike.Stage;
using Vamsurlike.UI;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Enemy
{
    public class EnemyNetworkBase : NetworkBehaviour
    {
        private static readonly Color BossTelegraphColor = new(1f, 0.1f, 0.05f, 0.9f);

        // 치명타 데미지 배율 — 기획 확정 전 기본값 (Phase 7.5)
        private const float CritDamageMultiplier = 1.5f;

        // RULES.md: 랜덤은 시드 기반 System.Random 인스턴스 사용
        private readonly System.Random critRng = new();

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

            // GAME_PLAN §8 공식: FinalDamage = amount * PlayerDamageMultiplier * (1 - EnemyDefenseRate)
            // PlayerDamageMultiplier는 SkillCastContext.FinalDamage에서 이미 적용된 상태로 amount에 실려 들어온다.
            float defense     = Mathf.Max(0f, data != null ? data.defense : 0f);
            float defenseRate = defense / (defense + 100f);
            float finalDamage = Mathf.Max(1f, amount * (1f - defenseRate));

            // 치명타 판정 — 플레이어 귀속 데미지에만 적용 (환경/디버그/아이템 비귀속 데미지는 크리티컬 없음)
            bool isCrit = false;
            if (attackerClientId != NoAttacker)
            {
                float critChance = GetPassiveStatHandler(attackerClientId)?.CritChance.Value ?? 0f;
                if (critChance > 0f && critRng.NextDouble() < critChance)
                {
                    isCrit = true;
                    finalDamage *= CritDamageMultiplier;
                }
            }

            HP.Value = Mathf.Max(0f, HP.Value - finalDamage);

            if (attackerClientId != NoAttacker)
            {
                lastAttackerClientId = attackerClientId;
                GetPlayerMatchStats(attackerClientId)?.AddDamage(finalDamage, skillTag);
                GetSkillManager(attackerClientId)?.AddUltimateGaugeForDamage(finalDamage);
            }

            float offset = data != null ? data.floatingTextHeightOffset : 2f;
            ShowDamageClientRpc(finalDamage, isCrit, transform.position + Vector3.up * offset);

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

        private PassiveStatHandler GetPassiveStatHandler(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return null;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            if (client.PlayerObject == null) return null;
            return client.PlayerObject.GetComponent<PassiveStatHandler>();
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
        private void ShowDamageClientRpc(float damage, bool isCrit, Vector3 worldPosition)
        {
            FloatingTextManager.Instance?.ShowDamage(damage, worldPosition, isCrit);
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
