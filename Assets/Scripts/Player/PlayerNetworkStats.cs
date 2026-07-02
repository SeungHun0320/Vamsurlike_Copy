using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;
using Vamsurlike.Stage;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(PlayerReviveHandler))]
    public class PlayerNetworkStats : ServerBehaviour
    {
        [SerializeField] private CharacterDataSO characterData;
        [SerializeField] private float fallbackMaxHP = 100f;
        [SerializeField] private float fallbackMoveSpeed = 5f;

        // 서버만 쓰고 모든 클라이언트가 읽는다.
        // ServerBehaviour로 컴포넌트가 disabled여도 NetworkVariable 동기화는 NGO 프레임워크가 처리.
        public NetworkVariable<float> MaxHP { get; } = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> HP { get; } = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> MoveSpeed { get; } = new(
            5f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> PickupRadius { get; } = new(
            2f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 방어력 — TakeDamage에서 §8 EnemyDefenseRate와 동일한 공식으로 피해 감소에 사용 (Phase 7.5)
        public NetworkVariable<float> Defense { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 초당 체력 재생량 — 패시브로만 증가, 기본 0 (Phase 7.5)
        public NetworkVariable<float> HealthRegenPerSecond { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsDowned { get; } = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 동료 부활 창 종료 후 자동 부활 대기 중 (이 기간에는 동료 부활 불가)
        public NetworkVariable<bool> IsDeadWaiting { get; } = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsAlive => HP.Value > 0f;

        // 이동/스킬 사용 가능 여부
        public bool CanAct => IsAlive && !IsDowned.Value && !IsDeadWaiting.Value;

        protected override void OnServerSpawned()
        {
            InitializeFromData(characterData);
        }

        public void InitializeFromData(CharacterDataSO data)
        {
            if (!EnsureServerAuthority(nameof(InitializeFromData))) return;

            float maxHP     = data != null ? data.baseHP        : fallbackMaxHP;
            float moveSpeed = data != null ? data.baseMoveSpeed : fallbackMoveSpeed;

            MaxHP.Value        = Mathf.Max(1f, maxHP);
            HP.Value           = MaxHP.Value;
            MoveSpeed.Value    = Mathf.Max(0f, moveSpeed);
            PickupRadius.Value = data != null ? data.basePickupRadius : 2f;
            Defense.Value      = data != null ? Mathf.Max(0f, data.baseDefense) : 0f;
        }

        // GAME_PLAN §8 EnemyDefenseRate와 동일한 공식 (defense/(defense+100)) — 플레이어 피격에도 동일 적용
        public void TakeDamage(float amount)
        {
            if (!EnsureServerAuthority(nameof(TakeDamage))) return;
            if (amount <= 0f || !IsAlive || IsDowned.Value) return;

            float defenseRate = Defense.Value / (Defense.Value + 100f);
            float finalDamage = Mathf.Max(1f, amount * (1f - defenseRate));

            HP.Value = Mathf.Max(0f, HP.Value - finalDamage);

            if (HP.Value <= 0f)
                GetComponent<PlayerReviveHandler>()?.BeginDowned();
        }

        public void Heal(float amount)
        {
            if (!EnsureServerAuthority(nameof(Heal))) return;
            if (amount <= 0f || !IsAlive) return;
            HP.Value = Mathf.Min(MaxHP.Value, HP.Value + amount);
        }

        // ServerBehaviour가 클라이언트 인스턴스에서 enabled=false 처리하므로 서버에서만 실행된다.
        private void Update()
        {
            if (HealthRegenPerSecond.Value <= 0f || !CanAct || HP.Value >= MaxHP.Value) return;
            if (GameFlowCoordinator.Instance == null || !GameFlowCoordinator.Instance.IsGameplayActive) return;

            HP.Value = Mathf.Min(MaxHP.Value, HP.Value + HealthRegenPerSecond.Value * Time.deltaTime);
        }
    }
}
