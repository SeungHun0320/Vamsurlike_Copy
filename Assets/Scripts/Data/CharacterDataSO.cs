using UnityEngine;

namespace Vamsurlike.Data
{
    [CreateAssetMenu(fileName = "CharacterData", menuName = "Vamsurlike/Data/Character")]
    public class CharacterDataSO : ScriptableObject
    {
        public string     characterName = "Player";
        public Sprite     portrait;
        public GameObject modelPrefab;

        [Header("Base Stats")]
        public float baseHP           = 100f;
        public float baseMoveSpeed    = 5f;
        public float baseAttackPower  = 10f;
        public float baseDefense      = 0f;
        public float basePickupRadius = 2f;

        [Header("Starting Skills")]
        public SkillDataSO[] startingSkills;
    }
}
