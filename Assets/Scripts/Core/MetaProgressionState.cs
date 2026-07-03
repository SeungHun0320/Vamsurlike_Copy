using System;
using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data.Runtime;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Core
{
    // Phase 7.6 — 골드/영구 업그레이드를 세션(앱 실행) 동안만 들고 있는 클라이언트 로컬 상태.
    // NetworkObject가 아니다. GameInstance가 DontDestroyOnLoad로 1개 보유.
    // 디스크 영속화는 Phase 9의 SaveManager가 담당 — 필드 구조를 그대로 직렬화할 수 있게 유지할 것.
    //
    // 해금 조건(스킬 누적 사용 횟수 등)은 일단 전부 뺐다 — Phase 9에서 SaveManager로 실제 영속화를
    // 붙일 때 "누적치가 세션 재시작으로 날아가면 의미 없다"는 문제까지 같이 설계하기로 함.
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
    }
}
