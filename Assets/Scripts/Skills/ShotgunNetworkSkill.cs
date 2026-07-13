using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 원뿔형 범위에 즉시 판정 후 사라지는 샷건(SpreadProjectile 재구성). 관통 없음 —
    // 실제 산탄총처럼 앞에 있는 적이 뒤에 있는 적을 가로막는다: 후보마다 시전자→적 방향으로
    // 레이캐스트를 쏴서 자기 자신보다 먼저 다른 적의 콜라이더에 막히면 데미지가 들어가지 않는다.
    // 맞는 인원수에 따라 데미지도 달라진다 — 한 명만 맞으면 집중사격으로 더 세게, 여러 명이 맞으면
    // 분산되어 덜 아프게(shotgunSoloDamageMultiplier / shotgunSharedDamageMultiplier).
    public sealed class ShotgunSkill : SkillBase
    {
        private const float MinToEnemySqrMagnitude = 0.0001f;
        // 적 콜라이더는 캡슐형(예: Enemy_A center.y=0.9)이라 시전자 발밑 높이(y≈0)에서 그대로 레이를
        // 쏘면 캡슐 바닥 끝점만 스치듯 지나가 거의 항상 빗나간다 — 몸통 높이로 올리고 두께를 줘서 보정.
        private const float RayHeight = 0.6f;
        private const float RayRadius = 0.4f;
        private static readonly int EnemyLayerMask = 1 << 7; // Layer 7: Enemy
        private static readonly Collider[] OverlapBuffer = new Collider[64];
        private static readonly List<EnemyNetworkBase> HitTargets = new(16);

        public override SkillCastType SupportedCastType => SkillCastType.Shotgun;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            Vector3 origin  = context.CasterTransform.position;
            float   range   = AutoTargeting.ResolveTargetingRange(context);
            Vector3 forward = AutoTargeting.ResolveDirection(context, origin, context.CasterForward, out _);
            float   coneAngle = Mathf.Max(0f, levelData.scatterAngle);
            float   halfArc   = coneAngle * 0.5f;
            float   damage  = context.FinalDamage;

            HitTargets.Clear();
            int overlapCount = Physics.OverlapSphereNonAlloc(origin, range, OverlapBuffer, EnemyLayerMask);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = OverlapBuffer[i];
                if (!col.TryGetComponent<EnemyNetworkBase>(out var enemy)) continue;

                Vector3 toEnemyFlat = col.transform.position - origin;
                toEnemyFlat.y = 0f;
                if (toEnemyFlat.sqrMagnitude < MinToEnemySqrMagnitude) continue;
                if (Vector3.Angle(forward, toEnemyFlat.normalized) > halfArc) continue;

                Vector3 rayOrigin = origin + Vector3.up * RayHeight;
                Vector3 rayTarget = col.transform.position + Vector3.up * RayHeight;
                Vector3 toEnemy   = rayTarget - rayOrigin;
                float   dist      = toEnemy.magnitude;
                bool blocked = Physics.SphereCast(rayOrigin, RayRadius, toEnemy / dist, out RaycastHit hit, dist, EnemyLayerMask)
                               && hit.collider != col;
                if (blocked) continue;

                HitTargets.Add(enemy);
            }

            if (HitTargets.Count > 0)
            {
                float multiplier = HitTargets.Count == 1
                    ? levelData.shotgunSoloDamageMultiplier
                    : levelData.shotgunSharedDamageMultiplier;
                float finalDamage = damage * multiplier;

                foreach (var enemy in HitTargets)
                    enemy.TakeDamage(finalDamage, context.OwnerClientId, skill.name);
            }

            context.VFX?.ShowMelee(SupportedCastType, origin, forward, range, coneAngle);
            return true;
        }
    }
}
