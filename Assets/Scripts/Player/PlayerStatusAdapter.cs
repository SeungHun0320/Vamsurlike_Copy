using Unity.Netcode;
using UnityEngine;
using Vamsurlike.UI.Events;

namespace Vamsurlike.Player
{
    // PlayerNetworkStats·PlayerReviveHandler의 NetworkVariable 변화를
    // UIEventHub.Player.PlayerStatusChanged 이벤트로 변환하는 Adapter.
    // 플레이어 프리팹에 부착. 모든 클라이언트에서 모든 플레이어 인스턴스에 대해 실행되며,
    // OwnerClientId로 어느 플레이어 상태인지 구분한다.
    [RequireComponent(typeof(PlayerNetworkStats))]
    [RequireComponent(typeof(PlayerReviveHandler))]
    [RequireComponent(typeof(PlayerMatchStats))]
    public class PlayerStatusAdapter : NetworkBehaviour
    {
        private PlayerNetworkStats  stats;
        private PlayerReviveHandler reviveHandler;
        private PlayerMatchStats    matchStats;

        private void Awake()
        {
            stats         = GetComponent<PlayerNetworkStats>();
            reviveHandler = GetComponent<PlayerReviveHandler>();
            matchStats    = GetComponent<PlayerMatchStats>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsClient) return; // 데디케이티드 서버에서는 UI 이벤트 불필요

            stats.HP.OnValueChanged                          += OnStatF;
            stats.MaxHP.OnValueChanged                       += OnStatF;
            stats.IsDowned.OnValueChanged                    += OnStatB;
            stats.IsDeadWaiting.OnValueChanged               += OnStatB;
            reviveHandler.DownedTimeRemaining.OnValueChanged += OnStatF;
            reviveHandler.DeadWaitRemaining.OnValueChanged   += OnStatF;
            matchStats.PlayerName.OnValueChanged             += OnNameChanged;

            Publish();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsClient) return;

            stats.HP.OnValueChanged                          -= OnStatF;
            stats.MaxHP.OnValueChanged                       -= OnStatF;
            stats.IsDowned.OnValueChanged                    -= OnStatB;
            stats.IsDeadWaiting.OnValueChanged               -= OnStatB;
            reviveHandler.DownedTimeRemaining.OnValueChanged -= OnStatF;
            reviveHandler.DeadWaitRemaining.OnValueChanged   -= OnStatF;
            matchStats.PlayerName.OnValueChanged             -= OnNameChanged;
        }

        private void OnStatF(float _, float __) => Publish();
        private void OnStatB(bool  _, bool  __) => Publish();
        private void OnNameChanged(Unity.Collections.FixedString64Bytes _, Unity.Collections.FixedString64Bytes __) => Publish();

        private void Publish()
        {
            if (UIEventHub.Instance == null) return;

            string name = matchStats.PlayerName.Value.ToString();
            if (string.IsNullOrWhiteSpace(name))
                name = $"Player {OwnerClientId + 1}";

            UIEventHub.Instance.Player.PublishPlayerStatus(new PlayerStatusPayload(
                clientId:            OwnerClientId,
                displayName:         name,
                hp:                  stats.HP.Value,
                maxHp:               stats.MaxHP.Value,
                isDowned:            stats.IsDowned.Value,
                isDeadWaiting:       stats.IsDeadWaiting.Value,
                isAlive:             stats.IsAlive,
                downedTimeRemaining: reviveHandler.DownedTimeRemaining.Value,
                deadWaitRemaining:   reviveHandler.DeadWaitRemaining.Value));
        }
    }
}
