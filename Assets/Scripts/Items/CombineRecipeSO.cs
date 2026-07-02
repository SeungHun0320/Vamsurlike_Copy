using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Items
{
    [CreateAssetMenu(fileName = "CombineRecipe_", menuName = "Vamsurlike/Combine Recipe")]
    public class CombineRecipeSO : ScriptableObject
    {
        [Tooltip("진화 재료 액티브 스킬 (만렙이어야 카드로 등장)")]
        public SkillDataSO sourceSkill;

        [Tooltip("진화 조건으로 요구되는 패시브 (1레벨 이상 보유만 하면 됨, 만렙 불필요)")]
        public UpgradeEffectType requiredPassiveType;

        [Tooltip("진화 결과 스킬")]
        public SkillDataSO evolvedSkill;

        public bool IsValid => sourceSkill != null && evolvedSkill != null;
    }
}
