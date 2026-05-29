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
        // 스킬 데미지 배율 — SkillBase가 읽어서 FinalDamage에 반영 (Phase 4 연동)
        public NetworkVariable<float> AttackMultiplier { get; } = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

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

        // 서버 전용: LevelUpManager.ApplyUpgrade에서 호출
        public void ApplyUpgrade(UpgradeOptionSO option)
        {
            if (!IsServer) return;
            if (option == null)
            {
                Debug.LogWarning($"[{nameof(PassiveStatHandler)}] 업그레이드 옵션이 null입니다.");
                return;
            }

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
                    if (stats != null)
                        stats.MoveSpeed.Value += option.value;
                    break;

                case UpgradeEffectType.PassivePickupRadius:
                    if (stats != null)
                        stats.PickupRadius.Value += option.value;
                    break;

                case UpgradeEffectType.PassiveAttackPower:
                    AttackMultiplier.Value += option.value;
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

            Debug.Log($"[{nameof(PassiveStatHandler)}] clientId {OwnerClientId} 업그레이드 적용: {option.upgradeName}");
        }
    }
}
