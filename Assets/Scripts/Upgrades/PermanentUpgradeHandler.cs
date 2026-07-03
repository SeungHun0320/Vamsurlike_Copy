using System;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Core;
using Vamsurlike.Data.Runtime;
using Vamsurlike.Player;

namespace Vamsurlike.Upgrades
{
    // Phase 7.6 — 클라이언트 로컬 메타 진행도 스냅샷을 서버 권한 플레이어 스탯에 적용한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PassiveStatHandler), typeof(PlayerNetworkStats))]
    public class PermanentUpgradeHandler : NetworkBehaviour
    {
        private PassiveStatHandler passiveStats;
        private PlayerNetworkStats playerStats;
        private Contributions applied;

        private struct Contributions
        {
            public float Damage;
            public float Defense;
            public float MaxHpBonus;
            public float HealthRegen;
            public float MoveSpeedBonus;
            public float PickupRadiusBonus;
            public float AreaBonus;
            public float DurationBonus;
            public float CritChance;
            public float SkillHaste;
            public float XpBonus;
            public int ProjectileCount;
            public float GoldBonus;
        }

        private void Awake()
        {
            passiveStats = GetComponent<PassiveStatHandler>();
            playerStats = GetComponent<PlayerNetworkStats>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
                SubmitLocalSnapshot();
        }

        public static bool TryApplyLocalPlayerSnapshot()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || networkManager.LocalClient == null || networkManager.LocalClient.PlayerObject == null)
                return false;

            if (!networkManager.LocalClient.PlayerObject.TryGetComponent(out PermanentUpgradeHandler handler))
                return false;

            handler.SubmitLocalSnapshot();
            return true;
        }

        public void SubmitLocalSnapshot()
        {
            if (!IsOwner) return;
            if (GameInstance.I == null) return;

            int[] snapshot = GameInstance.I.MetaProgression.BuildLevelSnapshot();
            SubmitPermanentUpgradeSnapshotServerRpc(snapshot);
        }

        [ServerRpc]
        private void SubmitPermanentUpgradeSnapshotServerRpc(int[] levels, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || levels == null) return;
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

            if (!DataManager.IsInitialized)
                DataManager.Initialize();

            Contributions next = BuildContributions(levels);
            ApplyDelta(next);
            applied = next;

            Debug.Log($"[{nameof(PermanentUpgradeHandler)}] 영구 업그레이드 적용 owner={OwnerClientId} dmg+{next.Damage:0.##} maxHp+{next.MaxHpBonus:P0} gold+{next.GoldBonus:P0}");
        }

        private static Contributions BuildContributions(int[] levels)
        {
            var result = new Contributions();
            var types = (PermanentUpgradeType[])Enum.GetValues(typeof(PermanentUpgradeType));
            for (int i = 0; i < types.Length && i < levels.Length; i++)
            {
                PermanentUpgradeType type = types[i];
                PermanentUpgradeData data = DataManager.GetPermanentUpgrade(type);
                if (data == null) continue;

                int level = Mathf.Clamp(levels[i], 0, data.MaxLevel);
                float value = data.PerLevelValue * level;

                switch (type)
                {
                    case PermanentUpgradeType.Damage:          result.Damage += value; break;
                    case PermanentUpgradeType.Defense:         result.Defense += value; break;
                    case PermanentUpgradeType.MaxHP:           result.MaxHpBonus += value; break;
                    case PermanentUpgradeType.HealthRegen:     result.HealthRegen += value; break;
                    case PermanentUpgradeType.MoveSpeed:       result.MoveSpeedBonus += value; break;
                    case PermanentUpgradeType.PickupRadius:    result.PickupRadiusBonus += value; break;
                    case PermanentUpgradeType.AreaSize:        result.AreaBonus += value; break;
                    case PermanentUpgradeType.Duration:        result.DurationBonus += value; break;
                    case PermanentUpgradeType.CritChance:      result.CritChance += value; break;
                    case PermanentUpgradeType.SkillHaste:      result.SkillHaste += value; break;
                    case PermanentUpgradeType.XPGain:          result.XpBonus += value; break;
                    case PermanentUpgradeType.ProjectileCount: result.ProjectileCount += Mathf.RoundToInt(value); break;
                    case PermanentUpgradeType.GoldGain:        result.GoldBonus += value; break;
                }
            }
            return result;
        }

        private void ApplyDelta(Contributions next)
        {
            if (passiveStats == null || playerStats == null) return;

            passiveStats.AttackMultiplier.Value += next.Damage - applied.Damage;
            passiveStats.MoveSpeedMultiplier.Value += next.MoveSpeedBonus - applied.MoveSpeedBonus;
            passiveStats.PickupRadiusMultiplier.Value += next.PickupRadiusBonus - applied.PickupRadiusBonus;
            passiveStats.AreaMultiplier.Value += next.AreaBonus - applied.AreaBonus;
            passiveStats.DurationMultiplier.Value += next.DurationBonus - applied.DurationBonus;
            passiveStats.XPMultiplier.Value += next.XpBonus - applied.XpBonus;
            passiveStats.SkillHaste.Value += next.SkillHaste - applied.SkillHaste;
            passiveStats.GoldMultiplier.Value += next.GoldBonus - applied.GoldBonus;
            passiveStats.CritChance.Value = Mathf.Clamp01(passiveStats.CritChance.Value + next.CritChance - applied.CritChance);
            passiveStats.PermanentBonusProjectileCount += next.ProjectileCount - applied.ProjectileCount;

            playerStats.Defense.Value += next.Defense - applied.Defense;
            playerStats.HealthRegenPerSecond.Value += next.HealthRegen - applied.HealthRegen;
            playerStats.ApplyMoveSpeedMultiplier(passiveStats.MoveSpeedMultiplier.Value);
            playerStats.ApplyPickupRadiusMultiplier(passiveStats.PickupRadiusMultiplier.Value);
            playerStats.ApplyMaxHPMultiplier(1f + next.MaxHpBonus);
        }
    }
}
