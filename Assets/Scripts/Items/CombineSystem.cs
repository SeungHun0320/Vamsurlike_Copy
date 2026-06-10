using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Skills;

namespace Vamsurlike.Items
{
    // 서버 전용. 카드 생성 전에 플레이어의 만렙 스킬과 레시피를 매칭해 진화/합체 카드 목록 반환.
    public static class CombineSystem
    {
        // 만렙 조건이 충족된 레시피 인덱스 목록 반환 (ChestChoiceType.Evolution 슬롯용)
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

                if (recipe.IsCombine)
                {
                    // 두 소스 스킬 모두 만렙이어야 합체 카드 등장
                    int level1 = skillManager.GetSkillLevel(recipe.sourceSkill);
                    int level2 = skillManager.GetSkillLevel(recipe.sourceSkill2);
                    if (level1 >= recipe.sourceSkill.maxLevel && level1 > 0
                        && level2 >= recipe.sourceSkill2.maxLevel && level2 > 0)
                        result.Add(new ChestChoiceData(ChestChoiceType.Evolution, i));
                }
                else
                {
                    int skillLevel = skillManager.GetSkillLevel(recipe.sourceSkill);
                    int maxLevel   = recipe.sourceSkill.maxLevel;
                    if (skillLevel >= maxLevel && skillLevel > 0)
                        result.Add(new ChestChoiceData(ChestChoiceType.Evolution, i));
                }
            }

            return result;
        }

        // 진화/합체 적용. 서버에서만 호출.
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

            return skillManager.EvolveSkill(recipe.sourceSkill, recipe.evolvedSkill, recipe.sourceSkill2);
        }
    }
}
