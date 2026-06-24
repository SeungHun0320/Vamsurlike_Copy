using Unity.Netcode;
using UnityEngine;
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

        public void AddXP(int amount)
        {
            if (!IsServer) return;
            if (amount <= 0)
            {
                Debug.LogWarning($"[{nameof(SharedLevelSystem)}] AddXP ignored invalid amount: {amount}");
                return;
            }

            SharedXP.Value += amount;
            CheckLevelUp();
        }

        internal void CheckLevelUp()
        {
            if (GameFlowCoordinator.Instance == null || GameFlowCoordinator.Instance.CurrentState.Value != GameState.Playing) return;

            int xpNeeded = XPRequired(SharedLevel.Value);
            if (SharedXP.Value < xpNeeded) return;

            if (levelUpManager == null || !levelUpManager.HasValidCatalog())
            {
                Debug.LogError($"[{nameof(SharedLevelSystem)}] UpgradeCatalog is missing or empty. Level-up blocked.");
                return;
            }

            SharedXP.Value -= xpNeeded;
            SharedLevel.Value++;
            levelUpManager.BeginLevelUp(SharedLevel.Value);
        }

        public static int XPRequired(int level) =>
            Mathf.RoundToInt(10f * Mathf.Pow(level, 1.5f));
    }
}
