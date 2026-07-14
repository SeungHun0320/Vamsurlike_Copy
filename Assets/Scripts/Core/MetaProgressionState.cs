using System;
using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data.Runtime;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Core
{
    // 골드/영구 업그레이드 상태를 담는 순수 데이터 클래스. NetworkObject가 아니다.
    // Phase 9부터 이 클래스의 진짜 권위 인스턴스는 서버(PlayerProgressionService)가 clientId별로
    // 들고 있으며, ServerSaveFileStore가 UGS PlayerId 기준으로 디스크에 영속화한다.
    // GameInstance가 DontDestroyOnLoad로 들고 있는 클라이언트 쪽 인스턴스는 서버가 보내주는
    // 동기화 메시지를 LoadFromSnapshot으로 반영만 하는 표시용 미러일 뿐이다.
    public sealed class MetaProgressionState
    {
        public event Action Changed;

        public int Gold { get; private set; }

        private readonly Dictionary<PermanentUpgradeType, int> upgradeLevels = new();

        public int GetLevel(PermanentUpgradeType type)
        {
            upgradeLevels.TryGetValue(type, out int level);
            return level;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            Changed?.Invoke();
        }

        // 구매 성공 시 true. 실패(골드 부족/만렙/데이터 없음) 시 false + 로그.
        public bool TryPurchase(PermanentUpgradeType type)
        {
            PermanentUpgradeData data = DataManager.GetPermanentUpgrade(type);
            if (data == null)
            {
                Debug.LogWarning($"[{nameof(MetaProgressionState)}] {type}: PermanentUpgradeTable에 데이터가 없습니다.");
                return false;
            }

            int currentLevel = GetLevel(type);
            if (currentLevel >= data.MaxLevel)
            {
                Debug.LogWarning($"[{nameof(MetaProgressionState)}] {type}: 이미 최대 레벨({data.MaxLevel})입니다.");
                return false;
            }

            int cost = data.GetCostForLevel(currentLevel + 1);
            if (Gold < cost)
            {
                Debug.LogWarning($"[{nameof(MetaProgressionState)}] {type}: 골드 부족 (보유 {Gold}, 필요 {cost}).");
                return false;
            }

            Gold -= cost;
            upgradeLevels[type] = currentLevel + 1;
            Debug.Log($"[{nameof(MetaProgressionState)}] {type} 구매 완료 — Lv.{currentLevel + 1}, 잔여 골드 {Gold}");
            Changed?.Invoke();
            return true;
        }

        // PermanentUpgradeHandler가 서버로 보낼 스냅샷 — PermanentUpgradeType 순서 고정 배열.
        public int[] BuildLevelSnapshot()
        {
            var values = (PermanentUpgradeType[])System.Enum.GetValues(typeof(PermanentUpgradeType));
            var snapshot = new int[values.Length];
            for (int i = 0; i < values.Length; i++)
                snapshot[i] = GetLevel(values[i]);
            return snapshot;
        }

        // 서버(ServerSaveFileStore)가 디스크에서 불러온 값을 복원하거나, 클라이언트가 서버의
        // 동기화 메시지를 표시용 미러에 반영할 때 사용.
        // TryPurchase와 달리 비용 검증 없이 그대로 적용 — 이미 정당하게 구매했던 상태를 복원하는 것이므로.
        public void LoadFromSnapshot(int gold, int[] levels)
        {
            Gold = Mathf.Max(0, gold);

            upgradeLevels.Clear();
            if (levels != null)
            {
                var values = (PermanentUpgradeType[])System.Enum.GetValues(typeof(PermanentUpgradeType));
                for (int i = 0; i < values.Length && i < levels.Length; i++)
                {
                    if (levels[i] > 0)
                        upgradeLevels[values[i]] = levels[i];
                }
            }

            Changed?.Invoke();
        }
    }
}
