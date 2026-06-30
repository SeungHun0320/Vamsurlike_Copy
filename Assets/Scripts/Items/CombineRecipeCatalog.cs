using UnityEngine;

namespace Vamsurlike.Items
{
    // Resources/Catalogs/CombineRecipeCatalog.asset 으로 배치.
    // 서버·클라이언트 동일 인덱스 순서 유지 — 인덱스가 레시피 ID 역할.
    [CreateAssetMenu(fileName = "CombineRecipeCatalog", menuName = "Vamsurlike/Combine Recipe Catalog")]
    public class CombineRecipeCatalog : ScriptableObject
    {
        private const string ResourcesPath = "Catalogs/CombineRecipeCatalog";

        private static CombineRecipeCatalog cachedInstance;
        public static CombineRecipeCatalog Instance
        {
            get
            {
                if (cachedInstance == null)
                    cachedInstance = Resources.Load<CombineRecipeCatalog>(ResourcesPath);
                return cachedInstance;
            }
        }

        public CombineRecipeSO[] recipes = System.Array.Empty<CombineRecipeSO>();

        public bool IsValidIndex(int index) =>
            index >= 0 && index < recipes.Length && recipes[index] != null;
    }
}
