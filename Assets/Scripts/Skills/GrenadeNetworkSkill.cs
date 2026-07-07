using System.Collections;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    // 서버: 포물선 시뮬레이션 + 착지 스플래시
    public sealed class GrenadeSkill : SkillBase
    {
        internal const float FlightTime = 0.6f;
        private const float SpawnHeightOffset = 0.8f;

        // RULES.md: 랜덤은 시드 기반 System.Random 인스턴스 사용
        private readonly System.Random rng = new();

        public override SkillCastType SupportedCastType => SkillCastType.Grenade;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null
                || levelData == null
                || context.CasterTransform == null
                || context.CoroutineRunner == null)
                return false;

            context.CoroutineRunner.StartSkillCoroutine(ThrowGrenadeCoroutine(
                context.CasterTransform.position,
                levelData,
                context.FinalDamage,
                context.OwnerClientId,
                context.Skill.name,
                context.AreaMultiplier,
                context.VFX));
            return true;
        }

        private IEnumerator ThrowGrenadeCoroutine(
            Vector3 origin,
            SkillLevelData levelData,
            float finalDamage,
            ulong ownerClientId,
            string skillTag,
            float areaMultiplier,
            ISkillVFXBroadcaster vfx)
        {
            double rx = rng.NextDouble() * 2.0 - 1.0;
            double rz = rng.NextDouble() * 2.0 - 1.0;
            double len = System.Math.Sqrt(rx * rx + rz * rz);
            if (len > 0.0001) { rx /= len; rz /= len; }

            float range = (float)(rng.NextDouble() * levelData.grenadeRange * areaMultiplier);
            Vector3 target = origin + new Vector3((float)rx * range, 0f, (float)rz * range);
            float splashRadius = levelData.splashRadius * areaMultiplier;

            Vector3 spawnPos = origin + Vector3.up * SpawnHeightOffset;
            vfx?.ShowGrenadeImpactCircle(target, splashRadius, FlightTime);
            vfx?.ShowGrenade(spawnPos, target, levelData.grenadeArcHeight, FlightTime);

            float elapsed = 0f;
            while (elapsed < FlightTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            SkillAreaDamage.ApplySplash(target, splashRadius, finalDamage, ownerClientId, skillTag);
        }
    }
}
