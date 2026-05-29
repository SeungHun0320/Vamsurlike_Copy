using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Stage
{
    // Stage 씬의 NetworkObject로 배치 (LevelUpManager와 같은 오브젝트).
    // XP/Level은 플레이어별이 아닌 게임 전체 공유.
    [RequireComponent(typeof(LevelUpManager))]
    public class SharedLevelSystem : NetworkBehaviour
    {
        public static SharedLevelSystem Instance { get; private set; }

        public NetworkVariable<float> SharedXP { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> SharedLevel { get; } = new(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private LevelUpManager levelUpManager;

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
            levelUpManager = GetComponent<LevelUpManager>();
            if (levelUpManager == null)
                Debug.LogError($"[{nameof(SharedLevelSystem)}] LevelUpManager 컴포넌트를 찾을 수 없습니다.", this);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        // 서버 전용: XPOrbManager.TryPickup에서 호출
        public void AddXP(int amount)
        {
            if (!IsServer) return;
            if (amount <= 0)
            {
                Debug.LogWarning($"[{nameof(SharedLevelSystem)}] AddXP 유효하지 않은 값: {amount}");
                return;
            }
            SharedXP.Value += amount;
            CheckLevelUp();
        }

        // LevelUpManager.FinalizeLevelUp에서도 호출 — Playing 복귀 후 누적 XP 재검사
        internal void CheckLevelUp()
        {
            if (StageRuntime.Instance == null || StageRuntime.Instance.CurrentState.Value != GameState.Playing) return;

            int xpNeeded = XPRequired(SharedLevel.Value);
            if (SharedXP.Value < xpNeeded) return;

            // Fix: 카탈로그 검증을 XP 차감 전에 수행 — 설정 실수 시 레벨만 오르고 UI 없이 보상 소실 방지
            if (levelUpManager == null || !levelUpManager.HasValidCatalog())
            {
                Debug.LogError($"[{nameof(SharedLevelSystem)}] UpgradeCatalog 없음 또는 옵션 없음 — 레벨업 차단. XP/Level 변경 없음.");
                return;
            }

            SharedXP.Value -= xpNeeded;
            SharedLevel.Value++;
            levelUpManager.BeginLevelUp(SharedLevel.Value);
        }

        // XPRequired(level): level에서 level+1로 가는 데 필요한 XP
        public static int XPRequired(int level) =>
            Mathf.RoundToInt(10f * Mathf.Pow(level, 1.5f));
    }
}
