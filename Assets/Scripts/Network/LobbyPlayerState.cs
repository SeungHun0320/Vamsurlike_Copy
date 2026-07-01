using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Network
{
    // 로비 단계에서 클라이언트 한 명의 상태를 네트워크 동기화.
    // 서버가 클라이언트 접속 시 스폰하며, 클라이언트 자신이 Owner로서 DisplayName·ColorIndex를 쓴다.
    // 게임 씬 전환 후에도 유지되며 플레이어 스폰 시 서버가 ColorIndex를 읽어 PlayerColorSync에 적용.
    public class LobbyPlayerState : NetworkBehaviour
    {
        private const string PrefKeyName = "PlayerName";

        // clientId → LobbyPlayerState 조회용 (서버·클라이언트 모두 사용)
        public static readonly Dictionary<ulong, LobbyPlayerState> All = new();

        public readonly NetworkVariable<FixedString64Bytes> DisplayName = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        public readonly NetworkVariable<int> ColorIndex = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            All[OwnerClientId] = this;

            if (!IsOwner) return;

            // Owner 클라이언트가 저장된 닉네임을 초기값으로 씀
            string savedName = PlayerPrefs.GetString(PrefKeyName, "").Trim();
            if (!string.IsNullOrEmpty(savedName))
                DisplayName.Value = new FixedString64Bytes(savedName);

            // 색상은 기본 0 (로비 UI에서 변경 예정)
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(OwnerClientId);
        }
    }
}
