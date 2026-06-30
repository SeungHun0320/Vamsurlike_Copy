using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Items
{
    [CreateAssetMenu(fileName = "ChestFallbackRewardCatalog", menuName = "Vamsurlike/Chest Fallback Reward Catalog")]
    public class ChestFallbackRewardCatalog : ScriptableObject
    {
        private const string ResourcesPath = "Catalogs/ChestFallbackRewardCatalog";

        private static ChestFallbackRewardCatalog cachedInstance;
        public static ChestFallbackRewardCatalog Instance
        {
            get
            {
                if (cachedInstance == null)
                    cachedInstance = Resources.Load<ChestFallbackRewardCatalog>(ResourcesPath);
                return cachedInstance;
            }
        }

        public ItemDataSO[] rewards = System.Array.Empty<ItemDataSO>();

        public bool IsValidIndex(int index) =>
            index >= 0 && index < rewards.Length && rewards[index] != null;
    }
}
