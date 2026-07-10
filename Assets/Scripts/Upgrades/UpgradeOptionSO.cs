using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Upgrades
{
    public enum UpgradeEffectType
    {
        PassiveMaxHP,
        PassiveMoveSpeed,
        PassiveAttackPower,
        PassivePickupRadius,
        SkillLevelUp, // 이미 보유한 스킬 레벨업 (미보유 시 습득)
        NewSkill,     // 새 스킬 습득 (이미 보유 시 레벨업)

        // Phase 7.5 추가분 — 기존 값 뒤에 추가해 UpgradeOptionSO 직렬화 값이 밀리지 않도록 한다.
        PassiveDefense,
        PassiveHealthRegen,
        PassiveAreaSize,
        PassiveDuration,
        PassiveXPGain,
        PassiveCritChance,
        PassiveSkillHaste,
        PassiveProjectileCount,
    }

    [CreateAssetMenu(fileName = "UpgradeOption_", menuName = "Vamsurlike/Upgrade Option")]
    public class UpgradeOptionSO : ScriptableObject
    {
        public string upgradeName;
        [TextArea] public string description;
        public Sprite icon;
        public UpgradeEffectType effectType;

        // PassiveStat 계열: 증가량
        public float value;

        // PassiveStat 계열 전용: 최대 픽 횟수 (SkillDataSO.maxLevel과 동일 역할).
        // SkillLevelUp/NewSkill은 대신 skillData.maxLevel을 사용하므로 이 필드를 참조하지 않는다.
        [Min(1)] public int maxLevel = 5;

        // SkillLevelUp / NewSkill: 대상 스킬 SO 직접 참조
        public SkillDataSO skillData;

#if UNITY_EDITOR
        private void OnValidate()
        {
            bool requiresSkillData = effectType is UpgradeEffectType.SkillLevelUp or UpgradeEffectType.NewSkill;
            if (requiresSkillData && skillData == null)
                Debug.LogWarning($"[{nameof(UpgradeOptionSO)}] \"{name}\": effectType={effectType}인데 skillData가 비어 있습니다.", this);
            else if (!requiresSkillData && skillData != null)
                Debug.LogWarning($"[{nameof(UpgradeOptionSO)}] \"{name}\": effectType={effectType}(패시브)인데 skillData(\"{skillData.name}\")가 설정되어 있습니다 — 사용되지 않는 참조입니다.", this);
        }
#endif
    }
}
