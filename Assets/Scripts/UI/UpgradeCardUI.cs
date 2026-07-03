using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.ViewModels;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI
{
    // UpgradeCard 프리팹에 배치. PermanentUpgradeShopUI가 그리드에 행마다 하나씩 Instantiate한다.
    public class UpgradeCardUI : MonoBehaviour
    {
        private static readonly Color CardColor         = new(0.10f, 0.12f, 0.15f, 0.96f);
        private static readonly Color CardSelectedColor = new(0.16f, 0.22f, 0.30f, 1f);
        private static readonly Color SegmentFillColor  = new(0.30f, 0.75f, 0.95f, 1f);
        private static readonly Color SegmentEmptyColor = new(0.24f, 0.26f, 0.30f, 1f);
        private static readonly Color GoldColor         = new(1f, 0.82f, 0.30f, 1f);
        private static readonly Color CostLockedColor   = new(0.62f, 0.34f, 0.34f, 1f);
        private static readonly Color NextValueColor    = new(0.42f, 0.90f, 0.55f, 1f);

        [SerializeField] private Image           background;
        [SerializeField] private Image           iconBackground;
        [SerializeField] private TextMeshProUGUI iconGlyph;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Transform       segmentsContainer;
        [SerializeField] private Button          button;

        private Image[] segments;

        private void Awake()
        {
            segments = segmentsContainer.GetComponentsInChildren<Image>(true);
        }

        public void Bind(PermanentUpgradeShopRow row, bool selected, Action<PermanentUpgradeType> onClick)
        {
            background.color = selected ? CardSelectedColor : CardColor;
            iconGlyph.text = PermanentUpgradeDisplay.GetIconGlyph(row.Type);
            iconGlyph.color = PermanentUpgradeDisplay.GetIconColor(row.Type);
            titleText.text = row.Name;
            costText.text = row.IsMaxed ? "MAX" : $"{row.Cost}G";
            costText.color = row.IsMaxed ? NextValueColor : (row.CanPurchase ? GoldColor : CostLockedColor);

            for (int i = 0; i < segments.Length; i++)
            {
                bool exists = i < row.MaxLevel;
                segments[i].gameObject.SetActive(exists);
                if (exists) segments[i].color = i < row.Level ? SegmentFillColor : SegmentEmptyColor;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick(row.Type));
        }
    }
}
