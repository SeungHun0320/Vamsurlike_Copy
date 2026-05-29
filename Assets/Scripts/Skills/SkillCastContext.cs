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
            float spawnForwardOffset,
            float attackMultiplier = 1f)
        {
            Manager = manager;
            Skill = skill;
            LevelData = levelData;
            Level = level;
            OwnerClientId = ownerClientId;
            CasterTransform = casterTransform;
            ProjectileSpawnPoint = projectileSpawnPoint;
            SpawnForwardOffset = spawnForwardOffset;
            AttackMultiplier = Mathf.Max(0f, attackMultiplier);
        }

        public SkillManager    Manager              { get; }
        public SkillDataSO     Skill                { get; }
        public SkillLevelData  LevelData            { get; }
        public int             Level                { get; }
        public ulong           OwnerClientId        { get; }
        public Transform       CasterTransform      { get; }
        public Transform       ProjectileSpawnPoint { get; }
        public float           SpawnForwardOffset   { get; }
        public float           AttackMultiplier     { get; }

        // 패시브 공격력 배율이 적용된 최종 데미지
        public float FinalDamage => LevelData != null ? LevelData.damage * AttackMultiplier : 0f;
    }
}
