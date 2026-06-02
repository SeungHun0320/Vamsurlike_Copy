using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Skills;

namespace Vamsurlike.Items
{
    // 서버 전용. 카드 생성 전에 플레이어의 만렙 스킬과 레시피를 매칭해 진화 카드 목록 반환.
    public static class CombineSystem
    {
        // SkillManager에서 만렙인 스킬의 진화 카드 인덱스 목록을 반환.
        // 반환값: CombineRecipeCatalog 인덱스 배열 (진화 카드 슬롯에 배치됨)
        public static List<ChestChoiceData> GetEvolutionCards(SkillManager skillManager)
        {
            var result = new List<ChestChoiceData>();

            var catalog = CombineRecipeCatalog.Instance;
            if (catalog == null || catalog.recipes == null) return result;

            for (int i = 0; i < catalog.recipes.Length; i++)
            {
                var recipe = catalog.recipes[i];
                if (recipe == null || !recipe.IsValid) continue;
                if (skillManager == null) continue;

                int skillLevel = skillManager.GetSkillLevel(recipe.sourceSkill);
                int maxLevel   = recipe.sourceSkill.maxLevel;

                if (skillLevel >= maxLevel && skillLevel > 0)
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
