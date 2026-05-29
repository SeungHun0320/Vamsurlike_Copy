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
        NewSkill      // 새 스킬 습득 (이미 보유 시 레벨업)
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
