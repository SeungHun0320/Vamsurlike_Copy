using System.Collections;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 합체 스킬: ScatterShot(기관총) + SkillHaste.
    // 기관총과 동일한 연속 점사(burst-fire) 방식이되, 스킬가속 수치만큼 발사 간격이 짧아져
    // 발사속도(총알 속도가 아니라 초당 발사 횟수)가 극단적으로 빨라진다.
    public sealed class GaleSpreadSkill : SkillBase
    {
        private const float DefaultSpawnHeight = 0.8f;

        // RULES.md: 시드 기반 System.Random 사용
        private readonly System.Random rng = new();

        // 연속 발사 중 중복 burst 방지
        private bool isBurstActive;

        public override SkillCastType SupportedCastType => SkillCastType.GaleSpread;
        public override bool IsPersistentExecution => false;

        protected override bool Execute(in SkillCastContext context, Vector3 direction, EnemyNetworkBase _)
        {
            if (isBurstActive) return false;

            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(GaleSpreadSkill)}] projectilePrefab 미할당. skill={skill.name}");
                return false;
            }

            isBurstActive = true;
            context.CoroutineRunner.StartSkillCoroutine(FireBurstCoroutine(
                skill.projectilePrefab, levelData, context.FinalDamage,
                context.OwnerClientId, context.CasterTransform, direction, skill.name, context.SpeedMultiplier,
                context.BonusProjectileCount, context.SkillHasteValue));

            return true;
        }

        private IEnumerator FireBurstCoroutine(
            GameObject prefab, SkillLevelData levelData, float finalDamage,
            ulong ownerClientId, Transform casterTransform, Vector3 baseForward, string skillName,
            float speedMultiplier, int bonusProjectileCount, float skillHasteValue)
        {
            // 스킬가속 15당 총알 1발 추가 (최대치 50 기준 +3발까지)
            int bonusFromHaste = Mathf.FloorToInt(Mathf.Max(0f, skillHasteValue) / 15f);
            int count = Mathf.Max(1, levelData.scatterBulletCount + bonusProjectileCount + bonusFromHaste);
            float halfSpread = levelData.scatterAngle * 0.5f;
            float kickRange = levelData.scatterAngle / count * 2f;

            // 스킬가속 수치만큼 발사 간격이 짧아져 발사속도(초당 발사 횟수)가 빨라진다 — 총알 자체의
            // 이동 속도(projectileSpeed)는 건드리지 않는다.
            float hasteIntervalDivisor = 1f + Mathf.Max(0f, skillHasteValue) / 100f;
            float interval = count > 1 ? (levelData.burstDuration / (count - 1)) / hasteIntervalDivisor : 0f;

            float recoilAngle = 0f;

            for (int i = 0; i < count; i++)
            {
                if (casterTransform == null) break;

                float kick = (float)(rng.NextDouble() * kickRange - kickRange * 0.5f);
                recoilAngle = Mathf.Clamp(recoilAngle + kick, -halfSpread, halfSpread);

                Vector3 origin = casterTransform.position + Vector3.up * DefaultSpawnHeight;
                Vector3 dir = Quaternion.AngleAxis(recoilAngle, Vector3.up) * baseForward;
                ProjectileSpawnHelper.Spawn(
                    prefab, ownerClientId, origin, dir, levelData,
                    finalDamage, speedMultiplier, skillName, nameof(GaleSpreadSkill));

                if (i < count - 1 && interval > 0f)
                    yield return new WaitForSeconds(interval);
            }

            isBurstActive = false;
        }
    }
}
