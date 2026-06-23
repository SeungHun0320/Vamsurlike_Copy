using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 합체 스킬: DamageAura + Hammer.
    // 가장 가까운 적 위치에 블랙홀 생성. duration 동안 areaRadius 내 적을 중심으로 끌어당기며
    // tickInterval마다 범위 내 적 전체에 데미지.
    public sealed class BlackHoleSkill : SkillBase
    {
        public override SkillCastType SupportedCastType => SkillCastType.BlackHole;

        protected override bool Execute(in SkillCastContext context, Vector3 direction, EnemyNetworkBase target)
        {
            SkillLevelData data = context.LevelData;

            // 타겟이 있으면 타겟 위치, 없으면 이동 방향 절반 사거리에 생성
            Vector3 center = target != null
                ? target.transform.position
                : context.CasterTransform.position + direction * (data.range * 0.5f);
            float pullRadius = data.areaRadius > 0f ? data.areaRadius : data.range;

            context.VFX?.ShowBlackHole(center, data.duration, pullRadius);
            context.CoroutineRunner.StartSkillCoroutine(
                RunBlackHole(center, data, context.FinalDamage));

            return true;
        }

        private static IEnumerator RunBlackHole(Vector3 center, SkillLevelData d, float damage)
        {
            var   targets    = new List<EnemyNetworkBase>();
            float elapsed    = 0f;
            float tickTimer  = 0f;
            float pullRadius = d.areaRadius > 0f ? d.areaRadius : d.range;

            while (elapsed < d.duration)
            {
                float dt = Time.deltaTime;
                elapsed   += dt;
                tickTimer += dt;

                int count = AutoTargeting.FindEnemiesInRange(center, pullRadius, targets);

                // 끌어당김 (매 프레임)
                for (int i = 0; i < count; i++)
                {
                    EnemyNetworkBase e = targets[i];
                    if (e == null) continue;
                    EnemyAI ai = e.GetComponent<EnemyAI>();
                    if (ai?.Agent == null || !ai.Agent.enabled || !ai.Agent.isOnNavMesh) continue;
                    Vector3 dir = center - e.transform.position;
                    dir.y = 0f;
                    if (dir.magnitude > 0.3f)
                        ai.Agent.Move(dir.normalized * d.pullSpeed * dt);
                }

                // 틱 데미지
                if (tickTimer >= d.tickInterval)
                {
                    tickTimer -= d.tickInterval;
                    for (int i = 0; i < count; i++)
                        targets[i]?.TakeDamage(damage);
                }

                yield return null;
            }
        }
    }
}
