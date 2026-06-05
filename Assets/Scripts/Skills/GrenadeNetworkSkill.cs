using System.Collections;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

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

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            context.Manager.StartSkillCoroutine(
                ThrowGrenadeCoroutine(context.CasterTransform.position, levelData, context.FinalDamage, context.Manager));
            return true;
        }

        private IEnumerator ThrowGrenadeCoroutine(Vector3 origin, SkillLevelData levelData, float finalDamage, SkillManager manager)
        {
            double rx = rng.NextDouble() * 2.0 - 1.0;
            double rz = rng.NextDouble() * 2.0 - 1.0;
            double len = System.Math.Sqrt(rx * rx + rz * rz);
            if (len > 0.0001) { rx /= len; rz /= len; }

            float range = (float)(rng.NextDouble() * levelData.grenadeRange);
            Vector3 target = origin + new Vector3((float)rx * range, 0f, (float)rz * range);

            Vector3 spawnPos = origin + Vector3.up * SpawnHeightOffset;
            manager.BroadcastGrenadeVFXClientRpc(spawnPos, target, levelData.grenadeArcHeight, FlightTime);

            float elapsed = 0f;
            while (elapsed < FlightTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplySplash(target, levelData.splashRadius, finalDamage);
        }

        private static void ApplySplash(Vector3 center, float radius, float damage)
        {
            var cols = Physics.OverlapSphere(center, radius);
            foreach (var col in cols)
            {
                if (col.TryGetComponent<EnemyNetworkBase>(out var enemy))
                    enemy.TakeDamage(damage);
            }
            Debug.Log($"[{nameof(GrenadeSkill)}] 착지 스플래시. center={center}, radius={radius}, damage={damage}");
        }
    }
}
