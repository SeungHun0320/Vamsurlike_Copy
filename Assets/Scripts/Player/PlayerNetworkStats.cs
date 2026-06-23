using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(PlayerReviveHandler))]
    public class PlayerNetworkStats : ServerBehaviour
    {
        [SerializeField] private CharacterDataSO characterData;
        [SerializeField] private float fallbackMaxHP = 100f;
        [SerializeField] private float fallbackMoveSpeed = 5f;

        // 서버만 쓰고 모든 클라이언트가 읽는다.
        // ServerBehaviour로 컴포넌트가 disabled여도 NetworkVariable 동기화는 NGO 프레임워크가 처리.
        public NetworkVariable<float> MaxHP { get; } = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> HP { get; } = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> MoveSpeed { get; } = new(
            5f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> PickupRadius { get; } = new(
            2f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsDowned { get; } = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsAlive => HP.Value > 0f;

        // 이동/스킬 사용 가능 여부 (살아있고 다운되지 않음)
        public bool CanAct => IsAlive && !IsDowned.Value;

        protected override void OnServerSpawned()
        {
            InitializeFromData(characterData);
        }

        public void InitializeFromData(CharacterDataSO data)
        {
            float maxHP     = data != null ? data.baseHP        : fallbackMaxHP;
            float moveSpeed = data != null ? data.baseMoveSpeed : fallbackMoveSpeed;

            MaxHP.Value        = Mathf.Max(1f, maxHP);
            HP.Value           = MaxHP.Value;
            MoveSpeed.Value    = Mathf.Max(0f, moveSpeed);
            PickupRadius.Value = data != null ? data.basePickupRadius : 2f;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || !IsAlive || IsDowned.Value) return;

            HP.Value = Mathf.Max(0f, HP.Value - amount);

            if (HP.Value <= 0f)
                GetComponent<PlayerReviveHandler>()?.BeginDowned();
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || !IsAlive) return;
            HP.Value = Mathf.Min(MaxHP.Value, HP.Value + amount);
        }
    }
}
