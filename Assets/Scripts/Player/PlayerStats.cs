using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Player
{
    public class PlayerStats : MonoBehaviour
    {
        [SerializeField] private CharacterDataSO characterData;

        [Header("Runtime Stats (read-only)")]
        [SerializeField] private float currentHP;
        [SerializeField] private float maxHP;
        [SerializeField] private float moveSpeed;
        [SerializeField] private float attackPower;
        [SerializeField] private float defense;
        [SerializeField] private float pickupRadius;

        public float HP           => currentHP;
        public float MaxHP        => maxHP;
        public float MoveSpeed    => moveSpeed;
        public float AttackPower  => attackPower;
        public float Defense      => defense;
        public float PickupRadius => pickupRadius;
        public bool  IsAlive      => currentHP > 0f;

        private void Awake()
        {
            if (characterData != null)
                InitFromData(characterData);
            else
            {
                maxHP        = 100f;
                currentHP    = 100f;
                moveSpeed    = 5f;
                attackPower  = 10f;
                defense      = 0f;
                pickupRadius = 2f;
            }
        }

        public void InitFromData(CharacterDataSO data)
        {
            characterData = data;
            maxHP         = data.baseHP;
            currentHP     = maxHP;
            moveSpeed     = data.baseMoveSpeed;
            attackPower   = data.baseAttackPower;
            defense       = data.baseDefense;
            pickupRadius  = data.basePickupRadius;
        }

        public void TakeDamage(float amount)
        {
            float dmg = Mathf.Max(0f, amount - defense);
            currentHP = Mathf.Max(0f, currentHP - dmg);
            Debug.Log($"[PlayerStats] Took {dmg:F1} dmg (raw {amount:F1}). HP: {currentHP:F1}/{maxHP:F1}");
        }

        public void Heal(float amount)
        {
            currentHP = Mathf.Min(maxHP, currentHP + amount);
        }

        public float GetStat(StatType type) => type switch
        {
            StatType.HP           => currentHP,
            StatType.MoveSpeed    => moveSpeed,
            StatType.AttackPower  => attackPower,
            StatType.Defense      => defense,
            StatType.PickupRadius => pickupRadius,
            _                     => 0f,
        };
    }
}
