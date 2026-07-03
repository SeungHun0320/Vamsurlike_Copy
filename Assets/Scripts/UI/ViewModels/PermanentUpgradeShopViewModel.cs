using System;
using System.Collections.Generic;
using Vamsurlike.Core;
using Vamsurlike.Data.Runtime;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI.ViewModels
{
    public sealed class PermanentUpgradeShopViewModel : IDisposable
    {
        public event Action Changed;

        public int Gold { get; private set; }
        public IReadOnlyList<PermanentUpgradeShopRow> Rows => rows;

        // 상세 패널에 표시 중인 항목. 그리드에서 카드를 클릭하면 바뀐다.
        public PermanentUpgradeType? SelectedType { get; private set; }

        private readonly List<PermanentUpgradeShopRow> rows = new();
        private MetaProgressionState Meta => GameInstance.I != null ? GameInstance.I.MetaProgression : null;

        public void Bind()
        {
            if (!DataManager.IsInitialized)
                DataManager.Initialize();
            if (Meta != null)
                Meta.Changed += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            if (Meta != null)
                Meta.Changed -= Refresh;
        }

        public void Refresh()
        {
            rows.Clear();
            Gold = Meta != null ? Meta.Gold : 0;

            foreach (PermanentUpgradeType type in Enum.GetValues(typeof(PermanentUpgradeType)))
            {
                PermanentUpgradeData data = DataManager.GetPermanentUpgrade(type);
                if (data == null) continue;

                int level = Meta != null ? Meta.GetLevel(type) : 0;
                bool maxed = level >= data.MaxLevel;
                int cost = maxed ? 0 : data.GetCostForLevel(level + 1);

                rows.Add(new PermanentUpgradeShopRow(
                    type,
                    PermanentUpgradeDisplay.GetName(type),
                    level,
                    data.MaxLevel,
                    cost,
                    maxed,
                    !maxed && Gold >= cost));
            }

            // 선택된 항목이 사라졌으면(이론상 없음) 첫 항목으로 대체
            if (SelectedType == null && rows.Count > 0)
                SelectedType = rows[0].Type;

            Changed?.Invoke();
        }

        public void SelectDetail(PermanentUpgradeType type)
        {
            if (SelectedType == type) return;
            SelectedType = type;
            Changed?.Invoke();
        }

        public bool TryGetDetail(out PermanentUpgradeDetail detail)
        {
            detail = default;
            if (SelectedType == null) return false;

            PermanentUpgradeType type = SelectedType.Value;
            PermanentUpgradeData data = DataManager.GetPermanentUpgrade(type);
            if (data == null) return false;

            int level = Meta != null ? Meta.GetLevel(type) : 0;
            bool maxed = level >= data.MaxLevel;
            int cost = maxed ? 0 : data.GetCostForLevel(level + 1);

            detail = new PermanentUpgradeDetail(
                type,
                PermanentUpgradeDisplay.GetName(type),
                PermanentUpgradeDisplay.GetDescription(type),
                PermanentUpgradeDisplay.FormatValue(type, data.PerLevelValue * level),
                maxed ? string.Empty : PermanentUpgradeDisplay.FormatValue(type, data.PerLevelValue * (level + 1)),
                level,
                data.MaxLevel,
                cost,
                maxed,
                !maxed && Gold >= cost);
            return true;
        }

        public bool TryPurchase(PermanentUpgradeType type)
        {
            bool purchased = Meta != null && Meta.TryPurchase(type);
            Refresh();
            return purchased;
        }

        public void DebugGrantGold(int amount)
        {
            Meta?.AddGold(amount);
            Refresh();
        }

        public bool ApplyToLocalPlayer()
        {
            return PermanentUpgradeHandler.TryApplyLocalPlayerSnapshot();
        }
    }

    public readonly struct PermanentUpgradeShopRow
    {
        public readonly PermanentUpgradeType Type;
        public readonly string Name;
        public readonly int Level;
        public readonly int MaxLevel;
        public readonly int Cost;
        public readonly bool IsMaxed;
        public readonly bool CanPurchase;

        public PermanentUpgradeShopRow(
            PermanentUpgradeType type, string name, int level, int maxLevel,
            int cost, bool isMaxed, bool canPurchase)
        {
            Type = type;
            Name = name;
            Level = level;
            MaxLevel = maxLevel;
            Cost = cost;
            IsMaxed = isMaxed;
            CanPurchase = canPurchase;
        }
    }

    // 상세 패널(우측) 전용 — 현재값 → 다음값 미리보기를 포함한다.
    public readonly struct PermanentUpgradeDetail
    {
        public readonly PermanentUpgradeType Type;
        public readonly string Name;
        public readonly string Description;
        public readonly string CurrentValueText;
        public readonly string NextValueText; // maxed면 빈 문자열
        public readonly int Level;
        public readonly int MaxLevel;
        public readonly int Cost;
        public readonly bool IsMaxed;
        public readonly bool CanPurchase;

        public PermanentUpgradeDetail(
            PermanentUpgradeType type, string name, string description,
            string currentValueText, string nextValueText,
            int level, int maxLevel, int cost, bool isMaxed, bool canPurchase)
        {
            Type = type;
            Name = name;
            Description = description;
            CurrentValueText = currentValueText;
            NextValueText = nextValueText;
            Level = level;
            MaxLevel = maxLevel;
            Cost = cost;
            IsMaxed = isMaxed;
            CanPurchase = canPurchase;
        }
    }

    // 이름/설명/수치 표기 — ViewModel과 UI(아이콘 심볼/색상)가 함께 참조하는 표시용 상수 모음.
    public static class PermanentUpgradeDisplay
    {
        public static string GetName(PermanentUpgradeType type) => type switch
        {
            PermanentUpgradeType.Damage => "피해량",
            PermanentUpgradeType.Defense => "방어력",
            PermanentUpgradeType.MaxHP => "최대 체력",
            PermanentUpgradeType.HealthRegen => "체력 재생",
            PermanentUpgradeType.MoveSpeed => "이동 속도",
            PermanentUpgradeType.PickupRadius => "획득 반경",
            PermanentUpgradeType.AreaSize => "범위",
            PermanentUpgradeType.Duration => "지속 시간",
            PermanentUpgradeType.CritChance => "치명타 확률",
            PermanentUpgradeType.SkillHaste => "스킬 가속",
            PermanentUpgradeType.XPGain => "경험치 획득",
            PermanentUpgradeType.ProjectileCount => "투사체 수",
            PermanentUpgradeType.GoldGain => "골드 획득",
            _ => type.ToString(),
        };

        public static string GetDescription(PermanentUpgradeType type) => type switch
        {
            PermanentUpgradeType.Damage => "스킬 피해량 배율을 곱해서 늘립니다.",
            PermanentUpgradeType.Defense => "받는 피해를 감소시킵니다.",
            PermanentUpgradeType.MaxHP => "최대 체력 배율을 곱해서 늘립니다.",
            PermanentUpgradeType.HealthRegen => "초당 체력 재생량을 늘립니다.",
            PermanentUpgradeType.MoveSpeed => "이동 속도 배율을 곱해서 늘립니다.",
            PermanentUpgradeType.PickupRadius => "골드/경험치 획득 반경을 늘립니다.",
            PermanentUpgradeType.AreaSize => "범위 판정 스킬의 크기를 늘립니다.",
            PermanentUpgradeType.Duration => "지속형 스킬의 유지 시간을 늘립니다.",
            PermanentUpgradeType.CritChance => "치명타 확률을 늘립니다.",
            PermanentUpgradeType.SkillHaste => "스킬 쿨다운을 감소시킵니다.",
            PermanentUpgradeType.XPGain => "경험치 획득 배율을 곱해서 늘립니다.",
            PermanentUpgradeType.ProjectileCount => "투사체 기반 스킬의 발사 수를 늘립니다.",
            PermanentUpgradeType.GoldGain => "골드 획득 배율을 곱해서 늘립니다.",
            _ => string.Empty,
        };

        // 카드/상세 패널 아이콘 자리 — 실제 아이콘 아트가 들어오기 전까지 텍스트+색상으로 대체.
        // 프로젝트 기본 TMP 폰트가 유니코드 딩뱃 심볼을 지원하지 않아(두부 현상) 한글 약어로 대체.
        public static string GetIconGlyph(PermanentUpgradeType type) => type switch
        {
            PermanentUpgradeType.Damage => "피해",
            PermanentUpgradeType.Defense => "방어",
            PermanentUpgradeType.MaxHP => "체력",
            PermanentUpgradeType.HealthRegen => "재생",
            PermanentUpgradeType.MoveSpeed => "속도",
            PermanentUpgradeType.PickupRadius => "반경",
            PermanentUpgradeType.AreaSize => "범위",
            PermanentUpgradeType.Duration => "지속",
            PermanentUpgradeType.CritChance => "치명",
            PermanentUpgradeType.SkillHaste => "가속",
            PermanentUpgradeType.XPGain => "경험",
            PermanentUpgradeType.ProjectileCount => "투사",
            PermanentUpgradeType.GoldGain => "골드",
            _ => "?",
        };

        public static UnityEngine.Color GetIconColor(PermanentUpgradeType type) => type switch
        {
            PermanentUpgradeType.Damage => new UnityEngine.Color(0.98f, 0.55f, 0.22f),
            PermanentUpgradeType.Defense => new UnityEngine.Color(0.35f, 0.78f, 0.90f),
            PermanentUpgradeType.MaxHP => new UnityEngine.Color(0.33f, 0.82f, 0.45f),
            PermanentUpgradeType.HealthRegen => new UnityEngine.Color(0.33f, 0.82f, 0.45f),
            PermanentUpgradeType.MoveSpeed => new UnityEngine.Color(0.55f, 0.75f, 0.98f),
            PermanentUpgradeType.PickupRadius => new UnityEngine.Color(0.70f, 0.62f, 0.95f),
            PermanentUpgradeType.AreaSize => new UnityEngine.Color(0.40f, 0.55f, 0.95f),
            PermanentUpgradeType.Duration => new UnityEngine.Color(0.40f, 0.55f, 0.95f),
            PermanentUpgradeType.CritChance => new UnityEngine.Color(0.95f, 0.35f, 0.45f),
            PermanentUpgradeType.SkillHaste => new UnityEngine.Color(0.98f, 0.82f, 0.30f),
            PermanentUpgradeType.XPGain => new UnityEngine.Color(0.98f, 0.82f, 0.30f),
            PermanentUpgradeType.ProjectileCount => new UnityEngine.Color(0.90f, 0.90f, 0.55f),
            PermanentUpgradeType.GoldGain => new UnityEngine.Color(0.95f, 0.75f, 0.25f),
            _ => UnityEngine.Color.white,
        };

        // Defense/HealthRegen/SkillHaste/ProjectileCount는 flat 가산, 나머지는 배율(%) 표기.
        public static string FormatValue(PermanentUpgradeType type, float total) => type switch
        {
            PermanentUpgradeType.Defense => $"+{total:0.#}",
            PermanentUpgradeType.HealthRegen => $"초당 +{total:0.#}",
            PermanentUpgradeType.SkillHaste => $"+{total:0.#}",
            PermanentUpgradeType.ProjectileCount => $"+{MathF.Round(total):0}",
            _ => $"x{1f + total:0.00}",
        };
    }
}
