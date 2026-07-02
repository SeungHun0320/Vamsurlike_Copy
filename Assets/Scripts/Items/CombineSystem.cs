using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Skills;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Items
{
    // 서버 전용. 카드 생성 전에 플레이어의 만렙 스킬 + 보유 패시브와 레시피를 매칭해 진화 카드 목록 반환.
    public static class CombineSystem
    {
        // 진화 조건: sourceSkill 만렙 보유 AND requiredPassiveType 1레벨 이상 보유
        public static List<ChestChoiceData> GetEvolutionCards(SkillManager skillManager, PassiveStatHandler passiveStatHandler)
        {
            var result = new List<ChestChoiceData>();

            var catalog = CombineRecipeCatalog.Instance;
            if (catalog == null || catalog.recipes == null) return result;
            if (skillManager == null) return result;

            for (int i = 0; i < catalog.recipes.Length; i++)
            {
                var recipe = catalog.recipes[i];
                if (recipe == null || !recipe.IsValid) continue;

                int skillLevel = skillManager.GetSkillLevel(recipe.sourceSkill);
                bool activeMaxed = skillLevel > 0 && skillLevel >= recipe.sourceSkill.maxLevel;
                if (!activeMaxed) continue;

                bool passiveOwned = passiveStatHandler != null && passiveStatHandler.HasPassive(recipe.requiredPassiveType);
                if (!passiveOwned) continue;

                result.Add(new ChestChoiceData(ChestChoiceType.Evolution, i));
            }

            return result;
        }

        // 진화 적용. 서버에서만 호출.
        public static bool TryEvolve(SkillManager skillManager, int recipeIndex)
        {
            var catalog = CombineRecipeCatalog.Instance;
            if (catalog == null || !catalog.IsValidIndex(recipeIndex)) return false;

            var recipe = catalog.recipes[recipeIndex];
            if (recipe == null || !recipe.IsValid)
            {
                Debug.LogWarning($"[{nameof(CombineSystem)}] 유효하지 않은 레시피 인덱스: {recipeIndex}");
                return false;
            }

            return skillManager.EvolveSkill(recipe.sourceSkill, recipe.evolvedSkill);
        }
    }
}
