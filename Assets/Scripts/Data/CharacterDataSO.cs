using UnityEngine;

namespace Vamsurlike.Data
{
    [CreateAssetMenu(fileName = "CharacterData", menuName = "Vamsurlike/Data/Character")]
    public class CharacterDataSO : ScriptableObject
    {
        public string m_strCharacterName = "Player";
        public Sprite m_sprPortrait;
        public GameObject m_goModelPrefab;

        [Header("Base Stats")]
        public float m_fBaseHP         = 100f;
        public float m_fBaseMoveSpeed  = 5f;
        public float m_fBaseAttackPower = 10f;
        public float m_fBaseDefense    = 0f;
        public float m_fBasePickupRadius = 2f;

        [Header("Starting Skills")]
        public SkillDataSO[] m_startingSkills;
    }
}
