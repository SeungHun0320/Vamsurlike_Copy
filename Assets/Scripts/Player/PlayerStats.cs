using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Player
{
    public class PlayerStats : MonoBehaviour
    {
        [SerializeField] private CharacterDataSO m_data;

        [Header("Runtime Stats (read-only)")]
        [SerializeField] private float m_fHP;
        [SerializeField] private float m_fMaxHP;
        [SerializeField] private float m_fMoveSpeed;
        [SerializeField] private float m_fAttackPower;
        [SerializeField] private float m_fDefense;
        [SerializeField] private float m_fPickupRadius;

        public float HP           => m_fHP;
        public float MaxHP        => m_fMaxHP;
        public float MoveSpeed    => m_fMoveSpeed;
        public float AttackPower  => m_fAttackPower;
        public float Defense      => m_fDefense;
        public float PickupRadius => m_fPickupRadius;
        public bool  IsAlive      => m_fHP > 0f;

        private void Awake()
        {
            if (m_data != null)
                InitFromData(m_data);
            else
            {
                m_fMaxHP        = 100f;
                m_fHP           = 100f;
                m_fMoveSpeed    = 5f;
                m_fAttackPower  = 10f;
                m_fDefense      = 0f;
                m_fPickupRadius = 2f;
            }
        }

        public void InitFromData(CharacterDataSO data)
        {
            m_data        = data;
            m_fMaxHP      = data.m_fBaseHP;
            m_fHP         = m_fMaxHP;
            m_fMoveSpeed  = data.m_fBaseMoveSpeed;
            m_fAttackPower = data.m_fBaseAttackPower;
            m_fDefense    = data.m_fBaseDefense;
            m_fPickupRadius = data.m_fBasePickupRadius;
        }

        public void TakeDamage(float raw)
        {
            float dmg = Mathf.Max(0f, raw - m_fDefense);
            m_fHP = Mathf.Max(0f, m_fHP - dmg);
            Debug.Log($"[PlayerStats] Took {dmg:F1} dmg (raw {raw:F1}). HP: {m_fHP:F1}/{m_fMaxHP:F1}");
        }

        public void Heal(float amount)
        {
            m_fHP = Mathf.Min(m_fMaxHP, m_fHP + amount);
        }

        public float GetStat(StatType type) => type switch
        {
            StatType.HP           => m_fHP,
            StatType.MoveSpeed    => m_fMoveSpeed,
            StatType.AttackPower  => m_fAttackPower,
            StatType.Defense      => m_fDefense,
            StatType.PickupRadius => m_fPickupRadius,
            _                     => 0f,
        };
    }
}
