using UnityEngine;

namespace Vamsurlike.Data
{
    public enum SkillCastType
    {
        Projectile,
        AreaAura,
        Orbital,
        Ultimate
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

        [Header("Persistent (Aura / Orbital)")]
        [Min(0f)] public float duration = 0f;       // 0 = 항상 활성
        [Min(0.01f)] public float tickInterval = 1f; // 활성 중 데미지 간격

        [Header("Area")]
        [Min(0f)] public float areaRadius = 0f;

        [Header("Orbital")]
        [Min(1)] public int orbitalCount = 1;
        [Min(0.1f)] public float orbitalRadius = 2f;
        [Min(0f)] public float orbitalRotationSpeed = 180f;
        [Min(0.05f)] public float orbitalHitRadius = 0.65f;

        [Header("Ultimate")]
        [Min(1)] public int waveCount = 1;
        [Min(0f)] public float waveDelay = 0.15f;
        [Min(0f)] public float rotationPerWave = 30f;
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
