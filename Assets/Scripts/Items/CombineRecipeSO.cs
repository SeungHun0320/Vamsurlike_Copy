using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Items
{
    [CreateAssetMenu(fileName = "CombineRecipe_", menuName = "Vamsurlike/Combine Recipe")]
    public class CombineRecipeSO : ScriptableObject
    {
        [Tooltip("합체/진화 재료 스킬 1 (만렙이어야 카드로 등장)")]
        public SkillDataSO sourceSkill;

        [Tooltip("합체 재료 스킬 2 (null = 단일 진화)")]
        public SkillDataSO sourceSkill2;

        [Tooltip("합체/진화 결과 스킬")]
        public SkillDataSO evolvedSkill;

        public bool IsValid    => sourceSkill != null && evolvedSkill != null;
        public bool IsCombine  => sourceSkill2 != null;
    }
}
