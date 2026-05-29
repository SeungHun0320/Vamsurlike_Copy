using UnityEngine;

namespace Vamsurlike.Upgrades
{
    // Resources/UpgradeCatalog.asset 으로 배치.
    // 서버·클라이언트 모두 동일 순서로 로드 — 인덱스가 옵션 ID 역할을 한다.
    [CreateAssetMenu(fileName = "UpgradeCatalog", menuName = "Vamsurlike/Upgrade Catalog")]
    public class UpgradeCatalog : ScriptableObject
    {
        private const string ResourcesPath = "UpgradeCatalog";

        private static UpgradeCatalog cachedInstance;
        public static UpgradeCatalog Instance
        {
            get
            {
                if (cachedInstance == null)
                    cachedInstance = Resources.Load<UpgradeCatalog>(ResourcesPath);
                return cachedInstance;
            }
        }

        public UpgradeOptionSO[] options = System.Array.Empty<UpgradeOptionSO>();

        public bool IsValidIndex(int index) =>
            index >= 0 && index < options.Length && options[index] != null;
    }
}
