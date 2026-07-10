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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (sourceSkill == null)
                Debug.LogWarning($"[{nameof(CombineRecipeSO)}] \"{name}\": sourceSkill이 비어 있습니다.", this);
            if (evolvedSkill == null)
                Debug.LogWarning($"[{nameof(CombineRecipeSO)}] \"{name}\": evolvedSkill이 비어 있습니다.", this);
            if (sourceSkill != null && evolvedSkill != null && sourceSkill == evolvedSkill)
                Debug.LogWarning($"[{nameof(CombineRecipeSO)}] \"{name}\": sourceSkill과 evolvedSkill이 같은 SkillDataSO입니다.", this);
            // 조합/진화 스킬은 1레벨=만렙으로 설계된다 — evolvedSkill.maxLevel이 1이 아니면 설계 이탈 신호.
            if (evolvedSkill != null && evolvedSkill.maxLevel != 1)
                Debug.LogWarning($"[{nameof(CombineRecipeSO)}] \"{name}\": evolvedSkill(\"{evolvedSkill.name}\")의 maxLevel이 {evolvedSkill.maxLevel}입니다. 조합 스킬은 보통 1레벨=만렙으로 설계됩니다.", this);
        }
#endif
    }
}
