using System.Text;
using UnityEngine;
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
                  .Append(opt.value).Append('|');
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
                sb.Append(recipe.sourceSkill  != null ? recipe.sourceSkill.name  : "null").Append('|')
                  .Append((int)recipe.requiredPassiveType).Append('|')
                  .Append(recipe.evolvedSkill != null ? recipe.evolvedSkill.name : "null").Append('|');
            }
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
