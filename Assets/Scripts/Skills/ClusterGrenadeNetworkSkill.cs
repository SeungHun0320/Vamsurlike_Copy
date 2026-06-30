using System.Collections;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    // 서버 전용. 착지 시 중심 스플래시 + clusterCount개 서브 그레네이드 분열.
    public sealed class ClusterGrenadeSkill : SkillBase
    {
        private const float FlightTime        = 0.6f;
        private const float SubFlightTime     = 0.35f;
        private const float SpawnHeightOffset = 0.8f;

        private readonly System.Random rng = new();

        public override SkillCastType SupportedCastType => SkillCastType.ClusterGrenade;

        public override bool TryExecute(in SkillCastContext context)
        {
            if (context.Skill == null
                || context.LevelData == null
                || context.CasterTransform == null
                || context.CoroutineRunner == null)
                return false;

            context.CoroutineRunner.StartSkillCoroutine(ThrowCoroutine(
                context.CasterTransform.position,
                context.LevelData,
                context.FinalDamage,
                context.OwnerClientId,
                context.Skill.name,
                context.CoroutineRunner,
                context.VFX));
            return true;
        }

        private IEnumerator ThrowCoroutine(
            Vector3 origin,
            SkillLevelData levelData,
            float finalDamage,
            ulong ownerClientId,
            string skillTag,
            ISkillCoroutineRunner coroutineRunner,
            ISkillVFXBroadcaster vfx)
        {
            Vector3 target = PickRandomTarget(origin, levelData.grenadeRange);

            Vector3 spawnPos = origin + Vector3.up * SpawnHeightOffset;
            vfx?.ShowGrenadeImpactCircle(target, levelData.splashRadius, FlightTime);
            vfx?.ShowGrenade(spawnPos, target, levelData.grenadeArcHeight, FlightTime);

            float elapsed = 0f;
            while (elapsed < FlightTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 중심 스플래시 (메인 데미지의 절반)
            float mainDamage = finalDamage * (1f - levelData.clusterDamageRatio);
            SkillAreaDamage.ApplySplash(target, levelData.splashRadius, mainDamage, ownerClientId, skillTag);

            // 서브 그레네이드 분열
            float subDamage = finalDamage * levelData.clusterDamageRatio;
            for (int i = 0; i < levelData.clusterCount; i++)
            {
                Vector3 subTarget = PickRandomTarget(target, levelData.clusterSpread);
                vfx?.ShowGrenadeImpactCircle(subTarget, levelData.clusterSplashRadius, SubFlightTime);
                vfx?.ShowGrenade(
                    target,
                    subTarget,
                    levelData.grenadeArcHeight * 0.4f,
                    SubFlightTime);
                coroutineRunner.StartSkillCoroutine(
                    SubGrenadeCoroutine(subTarget, levelData.clusterSplashRadius, subDamage, ownerClientId, skillTag));
            }
        }

        private IEnumerator SubGrenadeCoroutine(Vector3 target, float splashRadius, float damage, ulong ownerClientId, string skillTag)
        {
            float elapsed = 0f;
            while (elapsed < SubFlightTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            SkillAreaDamage.ApplySplash(target, splashRadius, damage, ownerClientId, skillTag);
        }

        private Vector3 PickRandomTarget(Vector3 origin, float maxRange)
        {
            double rx = rng.NextDouble() * 2.0 - 1.0;
            double rz = rng.NextDouble() * 2.0 - 1.0;
            double len = System.Math.Sqrt(rx * rx + rz * rz);
            if (len > 0.0001) { rx /= len; rz /= len; }
            float range = (float)(rng.NextDouble() * maxRange);
            return origin + new Vector3((float)rx * range, 0f, (float)rz * range);
        }

    }
}
