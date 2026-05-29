using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Player
{
    public class PlayerNetworkStats : NetworkBehaviour
    {
        [SerializeField] private CharacterDataSO characterData;
        [SerializeField] private float fallbackMaxHP = 100f;
        [SerializeField] private float fallbackMoveSpeed = 5f;

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

        public bool IsAlive => HP.Value > 0f;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            InitializeFromData(characterData);
        }

        public void InitializeFromData(CharacterDataSO data)
        {
            if (!IsServer) return;

            float maxHP      = data != null ? data.baseHP        : fallbackMaxHP;
            float moveSpeed  = data != null ? data.baseMoveSpeed : fallbackMoveSpeed;

            MaxHP.Value        = Mathf.Max(1f, maxHP);
            HP.Value           = MaxHP.Value;
            MoveSpeed.Value    = Mathf.Max(0f, moveSpeed);
            PickupRadius.Value = data != null ? data.basePickupRadius : 2f;
        }

        public void TakeDamage(float amount)
        {
            if (!IsServer) return;
            if (amount <= 0f || !IsAlive) return;

            HP.Value = Mathf.Max(0f, HP.Value - amount);
            Debug.Log($"[{nameof(PlayerNetworkStats)}] clientId {OwnerClientId} TakeDamage {amount} → HP {HP.Value}/{MaxHP.Value}");
        }

        public void Heal(float amount)
        {
            if (!IsServer) return;
            if (amount <= 0f || !IsAlive) return;

            HP.Value = Mathf.Min(MaxHP.Value, HP.Value + amount);
        }
    }
}
