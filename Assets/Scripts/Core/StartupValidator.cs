using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Items;
using Vamsurlike.Network;
using Vamsurlike.Stage;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Core
{
    // Bootstrap·Stage 진입 시 Inspector 참조 및 Resources SO를 일괄 검증한다.
    // 오류를 모두 출력한 뒤 valid 여부를 반환 — 첫 번째 오류에서 멈추지 않음.
    internal static class StartupValidator
    {
        internal static bool ValidateBootstrap(PoolManager poolManager)
        {
            bool valid = true;
            valid &= ValidateCatalogs();
            valid &= ValidatePoolManager(poolManager);

            if (valid)
                Debug.Log("[StartupValidator] Bootstrap 검증 완료.");
            else
                Debug.LogError("[StartupValidator] Bootstrap 검증 실패 — 위 오류를 확인하세요.");

            return valid;
        }

        internal static bool ValidateStage(WaveController waveController, DropManager dropManager)
        {
            bool valid = true;

            if (waveController == null)
            {
                Debug.LogError("[StartupValidator] StageRuntime에 WaveController가 할당되지 않았습니다.");
                valid = false;
            }

            if (dropManager == null)
            {
                Debug.LogError("[StartupValidator] StageRuntime에 DropManager가 할당되지 않았습니다.");
                valid = false;
            }

            if (valid)
                Debug.Log("[StartupValidator] Stage 검증 완료.");
            else
                Debug.LogError("[StartupValidator] Stage 검증 실패 — 위 오류를 확인하세요.");

            return valid;
        }

        private static bool ValidateCatalogs()
        {
            bool valid = true;

            if (UpgradeCatalog.Instance == null)
            {
                Debug.LogError("[StartupValidator] Resources/UpgradeCatalog.asset을 찾을 수 없습니다.");
                valid = false;
            }
            else
            {
                valid &= ValidateUpgradeCatalogIntegrity(UpgradeCatalog.Instance);
            }

            if (CombineRecipeCatalog.Instance == null)
            {
                Debug.LogError("[StartupValidator] Resources/CombineRecipeCatalog.asset을 찾을 수 없습니다.");
                valid = false;
            }
            else
            {
                valid &= ValidateRecipeCatalogIntegrity(CombineRecipeCatalog.Instance);
            }

            if (ChestFallbackRewardCatalog.Instance == null)
            {
                Debug.LogError("[StartupValidator] Resources/ChestFallbackRewardCatalog.asset을 찾을 수 없습니다.");
                valid = false;
            }
            else
            {
                valid &= ValidateChestCatalogIntegrity(ChestFallbackRewardCatalog.Instance);
            }

            return valid;
        }

        // 카탈로그 존재 여부만으로는 잡을 수 없는 내부 무결성 문제(참조 누락/중복/불일치)를 검증.
        private static bool ValidateUpgradeCatalogIntegrity(UpgradeCatalog catalog)
        {
            bool valid = true;
            var seenNames = new HashSet<string>();

            for (int i = 0; i < catalog.options.Length; i++)
            {
                UpgradeOptionSO opt = catalog.options[i];
                if (opt == null)
                {
                    Debug.LogError($"[StartupValidator] UpgradeCatalog.options[{i}]가 null입니다.");
                    valid = false;
                    continue;
                }

                if (!seenNames.Add(opt.upgradeName))
                {
                    Debug.LogError($"[StartupValidator] UpgradeCatalog에 upgradeName \"{opt.upgradeName}\"이 중복됩니다 (options[{i}]).");
                    valid = false;
                }

                bool requiresSkillData = opt.effectType is UpgradeEffectType.SkillLevelUp or UpgradeEffectType.NewSkill;
                if (requiresSkillData && opt.skillData == null)
                {
                    Debug.LogError($"[StartupValidator] UpgradeOptionSO \"{opt.name}\"은 effectType={opt.effectType}인데 skillData가 비어 있습니다.");
                    valid = false;
                }

                if (opt.skillData != null)
                    valid &= ValidateSkillData(opt.skillData, $"UpgradeOptionSO \"{opt.name}\"");
            }

            return valid;
        }

        private static bool ValidateRecipeCatalogIntegrity(CombineRecipeCatalog catalog)
        {
            bool valid = true;

            for (int i = 0; i < catalog.recipes.Length; i++)
            {
                CombineRecipeSO recipe = catalog.recipes[i];
                if (recipe == null)
                {
                    Debug.LogError($"[StartupValidator] CombineRecipeCatalog.recipes[{i}]가 null입니다.");
                    valid = false;
                    continue;
                }

                if (!recipe.IsValid)
                {
                    Debug.LogError($"[StartupValidator] CombineRecipeSO \"{recipe.name}\"의 sourceSkill/evolvedSkill 참조가 비어 있습니다.");
                    valid = false;
                    continue;
                }

                valid &= ValidateSkillData(recipe.sourceSkill,  $"CombineRecipeSO \"{recipe.name}\".sourceSkill");
                valid &= ValidateSkillData(recipe.evolvedSkill, $"CombineRecipeSO \"{recipe.name}\".evolvedSkill");

                if (recipe.evolvedSkill.maxLevel != 1)
                    Debug.LogWarning($"[StartupValidator] CombineRecipeSO \"{recipe.name}\".evolvedSkill(\"{recipe.evolvedSkill.name}\")은 조합 스킬인데 maxLevel이 1이 아닙니다 ({recipe.evolvedSkill.maxLevel}). 조합 스킬은 보통 1레벨=만렙으로 설계됩니다.");
            }

            return valid;
        }

        private static bool ValidateChestCatalogIntegrity(ChestFallbackRewardCatalog catalog)
        {
            bool valid = true;

            for (int i = 0; i < catalog.rewards.Length; i++)
            {
                if (catalog.rewards[i] == null)
                {
                    Debug.LogError($"[StartupValidator] ChestFallbackRewardCatalog.rewards[{i}]가 null입니다.");
                    valid = false;
                }
            }

            return valid;
        }

        private static bool ValidateSkillData(SkillDataSO skill, string context)
        {
            if (skill.levels == null || skill.levels.Length != skill.maxLevel)
            {
                int actual = skill.levels?.Length ?? 0;
                Debug.LogError($"[StartupValidator] {context}의 SkillDataSO \"{skill.name}\": maxLevel({skill.maxLevel})과 levels 배열 길이({actual})가 일치하지 않습니다.");
                return false;
            }

            return true;
        }

        private static bool ValidatePoolManager(PoolManager poolManager)
        {
            if (poolManager == null)
            {
                Debug.LogError("[StartupValidator] PoolManager를 찾을 수 없습니다.");
                return false;
            }

            return poolManager.Validate();
        }
    }
}
