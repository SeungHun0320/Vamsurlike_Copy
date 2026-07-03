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

        // 이동속도/획득반경/최대체력 기준치 — 배율 적용 시 매번 이 값 기준으로 다시 계산한다
        // (§7.5 배율 방식 통일, MaxHP는 §7.6 영구 업그레이드 전용 — 인게임 패시브는 여전히 flat 가산).
        private float baseMoveSpeed;
        private float basePickupRadius;
        private float baseMaxHP;

        protected override void OnServerSpawned()
        {
            InitializeFromData(characterData);
        }

        public void InitializeFromData(CharacterDataSO data)
        {
            if (!EnsureServerAuthority(nameof(InitializeFromData))) return;

            baseMaxHP        = Mathf.Max(1f, data != null ? data.baseHP : fallbackMaxHP);
            baseMoveSpeed    = Mathf.Max(0f, data != null ? data.baseMoveSpeed : fallbackMoveSpeed);
            basePickupRadius = data != null ? data.basePickupRadius : 2f;

            MaxHP.Value        = baseMaxHP;
            HP.Value           = MaxHP.Value;
            MoveSpeed.Value    = baseMoveSpeed;
            PickupRadius.Value = basePickupRadius;
            Defense.Value      = data != null ? Mathf.Max(0f, data.baseDefense) : 0f;
        }

        // PassiveStatHandler가 MoveSpeedMultiplier/PickupRadiusMultiplier를 갱신할 때마다 호출.
        // 기준치(baseMoveSpeed/basePickupRadius)에 배율을 다시 곱해 최종값을 갱신한다.
        public void ApplyMoveSpeedMultiplier(float multiplier)
        {
            if (!EnsureServerAuthority(nameof(ApplyMoveSpeedMultiplier))) return;
            MoveSpeed.Value = Mathf.Max(0f, baseMoveSpeed * multiplier);
        }

        public void ApplyPickupRadiusMultiplier(float multiplier)
        {
            if (!EnsureServerAuthority(nameof(ApplyPickupRadiusMultiplier))) return;
            PickupRadius.Value = Mathf.Max(0f, basePickupRadius * multiplier);
        }

        // §7.6 영구 업그레이드(MaxHP) 전용 — baseMaxHP 기준으로 배율 재계산, 현재 HP 비율은 유지한다.
        public void ApplyMaxHPMultiplier(float multiplier)
        {
            if (!EnsureServerAuthority(nameof(ApplyMaxHPMultiplier))) return;
            float ratio = MaxHP.Value > 0f ? HP.Value / MaxHP.Value : 1f;
            MaxHP.Value = Mathf.Max(1f, baseMaxHP * multiplier);
            if (IsAlive) HP.Value = Mathf.Max(1f, MaxHP.Value * ratio);
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
