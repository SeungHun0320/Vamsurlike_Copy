using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Items
{
    [CreateAssetMenu(fileName = "CombineRecipe_", menuName = "Vamsurlike/Combine Recipe")]
    public class CombineRecipeSO : ScriptableObject
    {
        [Tooltip("진화 전 스킬 (만렙이어야 진화 카드로 등장)")]
        public SkillDataSO sourceSkill;

        [Tooltip("진화 후 스킬 (기존 스킬 대체)")]
        public SkillDataSO evolvedSkill;

        public bool IsValid => sourceSkill != null && evolvedSkill != null;
    }
}
