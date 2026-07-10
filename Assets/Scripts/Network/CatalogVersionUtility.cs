using System.Text;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Items;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Network
{
    // 서버·클라이언트가 동일한 카탈로그 에셋을 가지고 있는지 확인하기 위한 해시 계산 유틸리티.
    // string.GetHashCode()는 플랫폼마다 결과가 다르므로 FNV-1a를 직접 구현한다.
    internal static class CatalogVersionUtility
    {
        private static int? cachedHash;

        internal static int GetHash()
        {
            cachedHash ??= ComputeHash();
            return cachedHash.Value;
        }

        private static int ComputeHash()
        {
            var sb = new StringBuilder();
            AppendUpgradeCatalog(sb);
            AppendRecipeCatalog(sb);
            AppendItemCatalog(sb);
            int hash = Fnv1a(sb.ToString());
            Debug.Log($"[{nameof(CatalogVersionUtility)}] 카탈로그 해시: {hash}");
            return hash;
        }

        private static void AppendUpgradeCatalog(StringBuilder sb)
        {
            var catalog = UpgradeCatalog.Instance;
            if (catalog == null)
            {
                Debug.LogWarning($"[{nameof(CatalogVersionUtility)}] UpgradeCatalog을 찾을 수 없습니다.");
                return;
            }

            foreach (var opt in catalog.options)
            {
                if (opt == null) { sb.Append("null|"); continue; }
                sb.Append(opt.name).Append('|')
                  .Append((int)opt.effectType).Append('|')
                  .Append(opt.value).Append('|')
                  .Append(opt.maxLevel).Append('|');
                AppendSkillData(sb, opt.skillData);
            }
        }

        private static void AppendRecipeCatalog(StringBuilder sb)
        {
            var catalog = CombineRecipeCatalog.Instance;
            if (catalog == null)
            {
                Debug.LogWarning($"[{nameof(CatalogVersionUtility)}] CombineRecipeCatalog을 찾을 수 없습니다.");
                return;
            }

            foreach (var recipe in catalog.recipes)
            {
                if (recipe == null) { sb.Append("null|"); continue; }
                AppendSkillData(sb, recipe.sourceSkill);
                sb.Append((int)recipe.requiredPassiveType).Append('|');
                AppendSkillData(sb, recipe.evolvedSkill);
            }
        }

        // 이름만 비교하면 SkillDataSO 내부 스탯(maxLevel, 레벨별 수치)이 바뀌어도 해시가
        // 그대로라 클라/서버 밸런스 불일치를 못 잡는다 — maxLevel과 레벨 배열 전체를 JSON으로
        // 직렬화해 포함시켜 필드가 늘어나도 자동으로 해시에 반영되게 한다.
        private static void AppendSkillData(StringBuilder sb, SkillDataSO skill)
        {
            if (skill == null) { sb.Append("null|"); return; }

            sb.Append(skill.name).Append('|')
              .Append((int)skill.castType).Append('|')
              .Append(skill.maxLevel).Append('|');

            if (skill.levels != null)
            {
                foreach (var level in skill.levels)
                    sb.Append(JsonUtility.ToJson(level)).Append(';');
            }
            sb.Append('|');
        }

        private static void AppendItemCatalog(StringBuilder sb)
        {
            var catalog = ChestFallbackRewardCatalog.Instance;
            if (catalog == null)
            {
                Debug.LogWarning($"[{nameof(CatalogVersionUtility)}] ChestFallbackRewardCatalog을 찾을 수 없습니다.");
                return;
            }

            foreach (var item in catalog.rewards)
                sb.Append(item != null ? item.name : "null").Append('|');
        }

        private static int Fnv1a(string text)
        {
            uint hash = 2166136261u;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 16777619u;
            }
            return (int)hash;
        }
    }
}
