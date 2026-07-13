using System.Collections;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    // 서버 전용. 착지 시 중심 스플래시 + clusterCount개 서브 그레네이드 분열.
    public sealed class ClusterGrenadeSkill : SkillBase
    {
        private const float FlightTime = 0.6f;  // 이동속도 배율 1.0(기본 이동속도) 기준 비행시간
        private const float SubFlightTime = 0.35f; // 이동속도 배율 1.0 기준 서브 그레네이드 비행시간
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

            // 기본 수류탄과 동일한 패턴 — 투사체 개수 증가 패시브는 "메인 수류탄을 몇 번 던지는가"
            // (grenadeCount)에만 가산한다. 착지 후 분열하는 서브 그레네이드 수(clusterCount)는
            // ThrowCoroutine 안에서 이 패시브의 영향을 받지 않고 레벨 데이터 값 그대로 쓴다.
            int count = Mathf.Max(1, context.LevelData.grenadeCount + context.BonusProjectileCount);
            for (int i = 0; i < count; i++)
            {
                context.CoroutineRunner.StartSkillCoroutine(ThrowCoroutine(
                    context.CasterTransform.position,
                    context.LevelData,
                    context.FinalDamage,
                    context.OwnerClientId,
                    context.Skill.name,
                    context.CoroutineRunner,
                    context.AreaMultiplier,
                    context.SpeedMultiplier,
                    context.VFX));
            }
            return true;
        }

        private IEnumerator ThrowCoroutine(
            Vector3 origin,
            SkillLevelData levelData,
            float finalDamage,
            ulong ownerClientId,
            string skillTag,
            ISkillCoroutineRunner coroutineRunner,
            float areaMultiplier,
            float speedMultiplier,
            ISkillVFXBroadcaster vfx)
        {
            float grenadeRange = levelData.grenadeRange * areaMultiplier;
            float splashRadius = levelData.splashRadius * areaMultiplier;
            float clusterSpread = levelData.clusterSpread * areaMultiplier;
            float clusterSplashRadius = levelData.clusterSplashRadius * areaMultiplier;
            // 튀는 서브 그레네이드 개수는 투사체 개수 증가 패시브의 영향을 받지 않는다 — 레벨 데이터의
            // clusterCount 그대로 고정.
            int clusterCount = Mathf.Max(0, levelData.clusterCount);
            // 이동속도 패시브가 투척 속도에도 반영되도록 비행시간을 반비례로 줄인다 — 메인/서브
            // 그레네이드 둘 다 이동속도가 빠를수록 더 빠르게 날아가 더 빨리 떨어진다.
            float speedScale = Mathf.Max(0.1f, speedMultiplier);
            float flightTime = FlightTime / speedScale;
            float subFlightTime = SubFlightTime / speedScale;

            Vector3 target = PickRandomTarget(origin, grenadeRange);

            Vector3 spawnPos = origin + Vector3.up * SpawnHeightOffset;
            vfx?.ShowGrenadeImpactCircle(target, splashRadius, flightTime);
            vfx?.ShowGrenade(spawnPos, target, levelData.grenadeArcHeight, flightTime);

            float elapsed = 0f;
            while (elapsed < flightTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 중심 스플래시 (메인 데미지의 절반)
            float mainDamage = finalDamage * (1f - levelData.clusterDamageRatio);
            SkillAreaDamage.ApplySplash(target, splashRadius, mainDamage, ownerClientId, skillTag);

            // 서브 그레네이드 분열
            float subDamage = finalDamage * levelData.clusterDamageRatio;
            for (int i = 0; i < clusterCount; i++)
            {
                Vector3 subTarget = PickRandomTarget(target, clusterSpread);
                vfx?.ShowGrenadeImpactCircle(subTarget, clusterSplashRadius, subFlightTime);
                vfx?.ShowGrenade(
                    target,
                    subTarget,
                    levelData.grenadeArcHeight * 0.4f,
                    subFlightTime);
                coroutineRunner.StartSkillCoroutine(
                    SubGrenadeCoroutine(subTarget, clusterSplashRadius, subDamage, ownerClientId, skillTag, subFlightTime));
            }
        }

        private IEnumerator SubGrenadeCoroutine(Vector3 target, float splashRadius, float damage, ulong ownerClientId, string skillTag, float subFlightTime)
        {
            float elapsed = 0f;
            while (elapsed < subFlightTime)
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
