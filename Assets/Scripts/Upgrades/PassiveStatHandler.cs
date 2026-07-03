using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Player;
using Vamsurlike.Skills;

namespace Vamsurlike.Upgrades
{
    // NetworkedPlayer 프리팹에 추가. 플레이어별 업그레이드 효과 적용.
    [RequireComponent(typeof(PlayerNetworkStats))]
    public class PassiveStatHandler : NetworkBehaviour
    {
        // 투사체 개수 패시브는 레벨당 균일 가산이 아니라 구간별로 증가한다 (기획 수치).
        // index = passiveLevels[PassiveProjectileCount] (0-based 누적 픽 횟수), value = 보너스 투사체 수.
        private static readonly int[] ProjectileCountByLevel = { 0, 1, 1, 2, 2, 3 };

        // 스킬 데미지 배율 — SkillBase가 읽어서 FinalDamage에 반영 (Phase 4 연동)
        public NetworkVariable<float> AttackMultiplier { get; } = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 이동속도/획득반경 배율 — 기본 1, PlayerNetworkStats.ApplyMoveSpeedMultiplier 등을 통해 반영 (Phase 7.5)
        public NetworkVariable<float> MoveSpeedMultiplier { get; } = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> PickupRadiusMultiplier { get; } = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 범위/유지시간 배율 — SkillCastContext를 통해 개별 스킬 실행부가 조회 (Phase 7.5)
        public NetworkVariable<float> AreaMultiplier { get; } = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> DurationMultiplier { get; } = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 경험치 습득 배율 — SharedLevelSystem.AddXP(amount, sourceClientId)가 조회
        public NetworkVariable<float> XPMultiplier { get; } = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 치명타 확률 (0~1) — EnemyNetworkBase.TakeDamage가 판정에 사용
        public NetworkVariable<float> CritChance { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 스킬 가속 — SkillExecutionScheduler 쿨다운 계산에 반영 (쿨다운 = 기본 / (1 + 가속/100))
        public NetworkVariable<float> SkillHaste { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 골드 획득 배율 — Phase 7.6 영구 업그레이드 전용 (인게임 패시브에는 대응 항목 없음).
        // GoldOrbManager.TryPickup이 조회한다.
        public NetworkVariable<float> GoldMultiplier { get; } = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Phase 7.6 영구 업그레이드(투사체)가 더하는 보너스 — PermanentUpgradeHandler가 스폰 시 1회 설정.
        // 인게임 패시브(ProjectileCountByLevel)와는 합산되어 적용된다.
        public int PermanentBonusProjectileCount { get; set; }

        // 투사체 기반 스킬에 더할 추가 투사체 수 (구간별 증가 — 위 ProjectileCountByLevel 참고 + 영구 업그레이드분)
        public int BonusProjectileCount
        {
            get
            {
                int level = GetPassiveLevel(UpgradeEffectType.PassiveProjectileCount);
                int index = Mathf.Clamp(level, 0, ProjectileCountByLevel.Length - 1);
                return ProjectileCountByLevel[index] + PermanentBonusProjectileCount;
            }
        }

        // 패시브 보유/레벨 조회 — 서버 전용, 조합 스킬 진화 조건 판정에 사용 (Phase 7.5)
        // NetworkVariable이 아닌 이유: 진화 조건 판정은 서버에서만 하므로 클라이언트 동기화가 불필요.
        private readonly Dictionary<UpgradeEffectType, int> passiveLevels = new();

        private PlayerNetworkStats stats;
        private SkillManager        skillManager;

        private void Awake()
        {
            stats        = GetComponent<PlayerNetworkStats>();
            skillManager = GetComponent<SkillManager>();

            if (stats == null)
                Debug.LogError($"[{nameof(PassiveStatHandler)}] PlayerNetworkStats 컴포넌트를 찾을 수 없습니다.", this);
            if (skillManager == null)
                Debug.LogWarning($"[{nameof(PassiveStatHandler)}] SkillManager 컴포넌트를 찾을 수 없습니다 — 스킬 업그레이드 불가.", this);
        }

        public bool HasPassive(UpgradeEffectType type) => GetPassiveLevel(type) > 0;

        public int GetPassiveLevel(UpgradeEffectType type)
        {
            passiveLevels.TryGetValue(type, out int level);
            return level;
        }

        // 서버 전용: LevelUpManager.ApplyUpgrade에서 호출
        public void ApplyUpgrade(UpgradeOptionSO option)
        {
            if (!IsServer) return;
            if (option == null)
            {
                Debug.LogWarning($"[{nameof(PassiveStatHandler)}] 업그레이드 옵션이 null입니다.");
                return;
            }

            if (IsPassiveStat(option.effectType))
                passiveLevels[option.effectType] = GetPassiveLevel(option.effectType) + 1;

            switch (option.effectType)
            {
                case UpgradeEffectType.PassiveMaxHP:
                    if (stats != null)
                    {
                        stats.MaxHP.Value += option.value;
                        // 죽은 플레이어는 HP를 올려도 부활하지 않는다 — 살아 있을 때만 회복
                        if (stats.IsAlive)
                            stats.HP.Value = Mathf.Min(stats.HP.Value + option.value, stats.MaxHP.Value);
                    }
                    break;

                case UpgradeEffectType.PassiveMoveSpeed:
                    MoveSpeedMultiplier.Value += option.value;
                    stats?.ApplyMoveSpeedMultiplier(MoveSpeedMultiplier.Value);
                    break;

                case UpgradeEffectType.PassivePickupRadius:
                    PickupRadiusMultiplier.Value += option.value;
                    stats?.ApplyPickupRadiusMultiplier(PickupRadiusMultiplier.Value);
                    break;

                case UpgradeEffectType.PassiveAttackPower:
                    AttackMultiplier.Value += option.value;
                    break;

                case UpgradeEffectType.PassiveDefense:
                    if (stats != null)
                        stats.Defense.Value += option.value;
                    break;

                case UpgradeEffectType.PassiveHealthRegen:
                    if (stats != null)
                        stats.HealthRegenPerSecond.Value += option.value;
                    break;

                case UpgradeEffectType.PassiveAreaSize:
                    AreaMultiplier.Value += option.value;
                    break;

                case UpgradeEffectType.PassiveDuration:
                    DurationMultiplier.Value += option.value;
                    break;

                case UpgradeEffectType.PassiveXPGain:
                    XPMultiplier.Value += option.value;
                    break;

                case UpgradeEffectType.PassiveCritChance:
                    CritChance.Value = Mathf.Clamp01(CritChance.Value + option.value);
                    break;

                case UpgradeEffectType.PassiveSkillHaste:
                    SkillHaste.Value += option.value;
                    break;

                case UpgradeEffectType.PassiveProjectileCount:
                    // 값은 passiveLevels 누적 횟수로만 결정 (BonusProjectileCount 참고). option.value는 사용하지 않는다.
                    break;

                case UpgradeEffectType.SkillLevelUp:
                    if (option.skillData == null)
                    {
                        Debug.LogWarning($"[{nameof(PassiveStatHandler)}] SkillLevelUp: skillData가 비어 있습니다. ({option.upgradeName})");
                        break;
                    }
                    if (skillManager == null)
                    {
                        Debug.LogWarning($"[{nameof(PassiveStatHandler)}] SkillManager 없음 — 스킬 레벨업 불가.");
                        break;
                    }
                    skillManager.UpgradeSkill(option.skillData);
                    break;

                case UpgradeEffectType.NewSkill:
                    if (option.skillData == null)
                    {
                        Debug.LogWarning($"[{nameof(PassiveStatHandler)}] NewSkill: skillData가 비어 있습니다. ({option.upgradeName})");
                        break;
                    }
                    if (skillManager == null)
                    {
                        Debug.LogWarning($"[{nameof(PassiveStatHandler)}] SkillManager 없음 — 스킬 습득 불가.");
                        break;
                    }
                    skillManager.LearnSkill(option.skillData);
                    break;

                default:
                    Debug.LogWarning($"[{nameof(PassiveStatHandler)}] 처리되지 않은 업그레이드 타입: {option.effectType}");
                    break;
            }

        }

        private static bool IsPassiveStat(UpgradeEffectType type)
        {
            return type != UpgradeEffectType.SkillLevelUp && type != UpgradeEffectType.NewSkill;
        }
    }
}
