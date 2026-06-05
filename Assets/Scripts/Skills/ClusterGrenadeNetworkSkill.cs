using System.Collections;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

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
            if (context.Skill == null || context.LevelData == null || context.CasterTransform == null)
                return false;

            context.Manager.StartSkillCoroutine(
                ThrowCoroutine(context.CasterTransform.position, context.LevelData, context.FinalDamage, context.Manager));
            return true;
        }

        private IEnumerator ThrowCoroutine(Vector3 origin, SkillLevelData levelData, float finalDamage, SkillManager manager)
        {
            Vector3 target = PickRandomTarget(origin, levelData.grenadeRange);

            Vector3 spawnPos = origin + Vector3.up * SpawnHeightOffset;
            manager.BroadcastGrenadeVFXClientRpc(spawnPos, target, levelData.grenadeArcHeight, FlightTime);

            float elapsed = 0f;
            while (elapsed < FlightTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 중심 스플래시 (메인 데미지의 절반)
            float mainDamage = finalDamage * (1f - levelData.clusterDamageRatio);
            ApplySplash(target, levelData.splashRadius, mainDamage);

            // 서브 그레네이드 분열
            float subDamage = finalDamage * levelData.clusterDamageRatio;
            for (int i = 0; i < levelData.clusterCount; i++)
            {
                Vector3 subTarget = PickRandomTarget(target, levelData.clusterSpread);
                manager.BroadcastGrenadeVFXClientRpc(target, subTarget, levelData.grenadeArcHeight * 0.4f, SubFlightTime);
                manager.StartSkillCoroutine(SubGrenadeCoroutine(subTarget, levelData.clusterSplashRadius, subDamage));
            }
        }

        private IEnumerator SubGrenadeCoroutine(Vector3 target, float splashRadius, float damage)
        {
            float elapsed = 0f;
            while (elapsed < SubFlightTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            ApplySplash(target, splashRadius, damage);
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

        private static void ApplySplash(Vector3 center, float radius, float damage)
        {
            var cols = Physics.OverlapSphere(center, radius);
            foreach (var col in cols)
            {
                if (col.TryGetComponent<EnemyNetworkBase>(out var enemy))
                    enemy.TakeDamage(damage);
            }
        }
    }
}
