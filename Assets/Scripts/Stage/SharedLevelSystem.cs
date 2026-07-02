using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Player;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Stage
{
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
                Debug.LogError($"[{nameof(SharedLevelSystem)}] LevelUpManager component is missing.", this);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        // 소유자 불명 호출(상자 폴백 XP, 디버그 명령 등) — 배율 없이 그대로 적용
        public void AddXP(int amount) => AddXP(amount, ulong.MaxValue);

        // sourceClientId: 오브를 주운 플레이어. XP는 여전히 공유 풀(SharedXP) 하나로 들어가지만,
        // 가산량 자체를 주운 플레이어의 PassiveStatHandler.XPMultiplier만큼 늘려서 넣는다.
        public void AddXP(int amount, ulong sourceClientId)
        {
            if (!IsServer) return;
            if (amount <= 0)
            {
                Debug.LogWarning($"[{nameof(SharedLevelSystem)}] AddXP ignored invalid amount: {amount}");
                return;
            }

            float multiplier = GetXPMultiplier(sourceClientId);
            int scaledAmount = Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));

            SharedXP.Value += scaledAmount;
            CheckLevelUp();
        }

        private static float GetXPMultiplier(ulong clientId)
        {
            if (clientId == ulong.MaxValue) return 1f;
            if (NetworkManager.Singleton == null) return 1f;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return 1f;
            if (client.PlayerObject == null) return 1f;
            return client.PlayerObject.TryGetComponent<PassiveStatHandler>(out var passive)
                ? passive.XPMultiplier.Value
                : 1f;
        }

        internal void CheckLevelUp()
        {
            if (GameFlowCoordinator.Instance == null || !GameFlowCoordinator.Instance.IsGameplayActive) return;

            int xpNeeded = XPRequired(SharedLevel.Value);
            if (SharedXP.Value < xpNeeded) return;

            if (levelUpManager == null || !levelUpManager.HasValidCatalog())
            {
                Debug.LogError($"[{nameof(SharedLevelSystem)}] UpgradeCatalog is missing or empty. Level-up blocked.");
                return;
            }

            SharedXP.Value -= xpNeeded;
            SharedLevel.Value++;
            SyncLevelToPlayers(SharedLevel.Value);
            levelUpManager.BeginLevelUp(SharedLevel.Value);
        }

        private static void SyncLevelToPlayers(int level)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                if (client.PlayerObject.TryGetComponent<PlayerMatchStats>(out var matchStats))
                    matchStats.SetLevel(level);
            }
        }

        public static int XPRequired(int level) =>
            Mathf.RoundToInt(10f * Mathf.Pow(level, 1.5f));
    }
}
