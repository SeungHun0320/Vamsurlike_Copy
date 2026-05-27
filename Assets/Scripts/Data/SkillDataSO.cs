using UnityEngine;

namespace Vamsurlike.Data
{
    public enum SkillCastType
    {
        Projectile,
        AreaAura
    }

    [System.Serializable]
    public class SkillLevelData
    {
        [Header("Common")]
        [Min(0.01f)] public float cooldown = 1f;
        [Min(0f)] public float damage = 10f;
        [Min(0.1f)] public float range = 10f;

        [Header("Projectile")]
        [Min(0.1f)] public float projectileSpeed = 12f;
        [Min(0.1f)] public float projectileLifetime = 2f;
        [Min(0.05f)] public float projectileHitRadius = 0.5f;
        [Min(1)] public int projectileCount = 1;
        [Min(0f)] public float spreadAngle = 0f;
        [Min(0)] public int pierceCount = 0;

        [Header("Area")]
        [Min(0f)] public float areaRadius = 0f;
        [Min(0.01f)] public float tickInterval = 1f;
    }

    [CreateAssetMenu(fileName = "SkillData", menuName = "Vamsurlike/Data/Skill")]
    public class SkillDataSO : ScriptableObject
    {
        public string skillName = "Skill";
        public Sprite icon;
        public SkillCastType castType = SkillCastType.Projectile;
        public bool isManual;

        [Header("Projectile")]
        public GameObject projectilePrefab;

        [Header("Levels")]
        [Min(1)] public int maxLevel = 1;
        public SkillLevelData[] levels =
        {
            new()
        };

        public SkillLevelData GetLevelData(int level)
        {
            if (levels == null || levels.Length == 0)
                return null;

            int index = Mathf.Clamp(level - 1, 0, levels.Length - 1);
            return levels[index];
        }
    }
}
