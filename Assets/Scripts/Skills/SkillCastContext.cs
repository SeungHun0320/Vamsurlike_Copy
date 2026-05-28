using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    public readonly struct SkillCastContext
    {
        public SkillCastContext(
            SkillManager manager,
            SkillDataSO skill,
            SkillLevelData levelData,
            int level,
            ulong ownerClientId,
            Transform casterTransform,
            Transform projectileSpawnPoint,
            float spawnForwardOffset)
        {
            Manager = manager;
            Skill = skill;
            LevelData = levelData;
            Level = level;
            OwnerClientId = ownerClientId;
            CasterTransform = casterTransform;
            ProjectileSpawnPoint = projectileSpawnPoint;
            SpawnForwardOffset = spawnForwardOffset;
        }

        public SkillManager Manager { get; }
        public SkillDataSO Skill { get; }
        public SkillLevelData LevelData { get; }
        public int Level { get; }
        public ulong OwnerClientId { get; }
        public Transform CasterTransform { get; }
        public Transform ProjectileSpawnPoint { get; }
        public float SpawnForwardOffset { get; }
    }
}
