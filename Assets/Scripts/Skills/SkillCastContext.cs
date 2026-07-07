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
            float speedMultiplier  = 1f,
            float areaMultiplier   = 1f,
            int   bonusProjectileCount = 0,
            float durationMultiplier = 1f)
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
            AreaMultiplier   = Mathf.Max(0f, areaMultiplier);
            BonusProjectileCount = Mathf.Max(0, bonusProjectileCount);
            DurationMultiplier = Mathf.Max(0f, durationMultiplier);
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
        // 범위 크기 패시브 배율 — 반경/사거리를 쓰는 스킬 실행부가 곱해서 사용 (Phase 7.5)
        public float           AreaMultiplier       { get; }
        // 투사체 개수 패시브 — 투사체 기반 스킬 실행부가 발사 수에 더해서 사용 (Phase 7.5)
        public int             BonusProjectileCount { get; }
        // 유지시간 패시브 배율 — 자체 코루틴으로 지속시간을 관리하는 스킬(블랙홀 등)이 곱해서 사용.
        // 지속형(Persistent) 스킬은 SkillExecutionScheduler가 별도로 적용하므로 여기서 중복 적용하지 않는다.
        public float           DurationMultiplier   { get; }

        // 패시브 공격력 배율이 적용된 최종 데미지
        public float FinalDamage => LevelData != null ? LevelData.damage * AttackMultiplier : 0f;
    }
}
