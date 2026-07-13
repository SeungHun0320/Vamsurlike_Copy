using UnityEngine;

namespace Vamsurlike.Data
{
    public enum ItemType
    {
        Chest,
        HealthOrb,
        Missile,
        Magnet // 픽업 시 씬에 남아있는 모든 XP 오브를 즉시 흡수
    }

    [CreateAssetMenu(fileName = "ItemData_", menuName = "Vamsurlike/Data/Item")]
    public class ItemDataSO : ScriptableObject
    {
        public string   itemName;
        public ItemType itemType;
        public Sprite   icon;
        public GameObject pickupPrefab;

        [Tooltip("체력 회복량(HealthOrb) 또는 미사일 데미지(Missile)")]
        public float value = 30f;

        [Tooltip("Missile 전용: 픽업 위치 기준 AoE 반경")]
        public float missileRadius = 30f;
    }
}
