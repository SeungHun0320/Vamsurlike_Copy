using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    public readonly struct SkillCastContext
    {
        public SkillCastContext(
            ISkillCoroutineRunner coroutineRunner,
            ISkillVFXBroadcaster vfx,
            SkillDataSO skill,
            SkillLevelData levelData,
            int level,
            ulong ownerClientId,
            Transform casterTransform,
            Transform projectileSpawnPoint,
            float spawnForwardOffset,
            Vector3 casterForward,
            float attackMultiplier = 1f,
            float speedMultiplier  = 1f)
        {
            CoroutineRunner = coroutineRunner;
            VFX = vfx;
            Skill = skill;
            LevelData = levelData;
            Level = level;
            OwnerClientId = ownerClientId;
            CasterTransform = casterTransform;
            ProjectileSpawnPoint = projectileSpawnPoint;
            SpawnForwardOffset = spawnForwardOffset;
            CasterForward = casterForward.sqrMagnitude > 0.0001f ? casterForward.normalized : Vector3.forward;
            AttackMultiplier = Mathf.Max(0f, attackMultiplier);
            SpeedMultiplier  = Mathf.Max(0f, speedMultiplier);
        }

        public ISkillCoroutineRunner CoroutineRunner { get; }
        public ISkillVFXBroadcaster VFX              { get; }
        public SkillDataSO     Skill                { get; }
        public SkillLevelData  LevelData            { get; }
        public int             Level                { get; }
        public ulong           OwnerClientId        { get; }
        public Transform       CasterTransform      { get; }
        public Transform       ProjectileSpawnPoint { get; }
        public float           SpawnForwardOffset   { get; }
        // 타겟 없을 때 스킬 발사 방향 폴백 (LastNonZeroMoveDirection 기반)
        public Vector3         CasterForward        { get; }
        public float           AttackMultiplier     { get; }
        public float           SpeedMultiplier      { get; }

        // 패시브 공격력 배율이 적용된 최종 데미지
        public float FinalDamage => LevelData != null ? LevelData.damage * AttackMultiplier : 0f;
    }
}
