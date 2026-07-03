using Vamsurlike.Upgrades;

namespace Vamsurlike.Data.Runtime
{
    // Phase 7.6 영구 업그레이드 1종의 레벨별 비용/효과 정의.
    // 레벨당 증가치는 고정(perLevelValue)이고, 비용은 레벨마다 baseCost * costGrowth^level로 증가한다.
    public sealed class PermanentUpgradeData
    {
        public int                  Id            { get; }
        public PermanentUpgradeType Type          { get; }
        public float                PerLevelValue { get; }
        public int                  MaxLevel      { get; }
        public int                  BaseCost      { get; }
        public float                CostGrowth    { get; }

        public PermanentUpgradeData(
            int id, PermanentUpgradeType type, float perLevelValue, int maxLevel, int baseCost, float costGrowth)
        {
            Id            = id;
            Type          = type;
            PerLevelValue = perLevelValue;
            MaxLevel      = maxLevel;
            BaseCost      = baseCost;
            CostGrowth    = costGrowth;
        }

        // level: 구매 후 도달하는 레벨(1부터). 다음 구매 비용을 조회할 때 currentLevel+1을 넘긴다.
        public int GetCostForLevel(int level)
        {
            if (level <= 0) return 0;
            double cost = BaseCost * System.Math.Pow(CostGrowth, level - 1);
            return UnityEngine.Mathf.RoundToInt((float)cost);
        }
    }
}
