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

        // SkillLevelUp / NewSkill: 대상 스킬 SO 직접 참조
        public SkillDataSO skillData;
    }
}
