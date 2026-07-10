using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 합체 스킬: PierceProjectile + MoveSpeed.
    // 전방으로 전진하며 관통한 적마다 스택을 쌓고, 최대 사거리에서 방향을 반전해 귀환한다.
    // 귀환 시에는 스택 수만큼 증폭된 데미지로 다시 타격한다. 실제 NetworkProjectile을 스폰하지 않고
    // Grenade류 스킬처럼 서버 코루틴이 직접 가상의 이동 지점을 시뮬레이션한다.
    public sealed class PiercingBoomerangSkill : SkillBase
    {
        private const float SpawnHeightOffset = 0.8f;
        private static readonly Collider[] OverlapBuffer = new Collider[128];

        public override SkillCastType SupportedCastType => SkillCastType.PiercingBoomerang;

        protected override bool Execute(in SkillCastContext context, Vector3 direction, EnemyNetworkBase _)
        {
            SkillLevelData levelData = context.LevelData;
            if (levelData == null || context.CoroutineRunner == null || context.CasterTransform == null)
                return false;

            Vector3 origin = context.CasterTransform.position + Vector3.up * SpawnHeightOffset;

            context.CoroutineRunner.StartSkillCoroutine(FlyCoroutine(
                context.CasterTransform, origin, direction, levelData, context.FinalDamage,
                context.OwnerClientId, context.Skill.name, context.SpeedMultiplier, context.AreaMultiplier, context.VFX));

            return true;
        }

        private IEnumerator FlyCoroutine(
            Transform casterTransform,
            Vector3 origin,
            Vector3 direction,
            SkillLevelData levelData,
            float baseDamage,
            ulong ownerClientId,
            string skillTag,
            float speedMultiplier,
            float areaMultiplier,
            ISkillVFXBroadcaster vfx)
        {
            float speed = Mathf.Max(0.1f, levelData.projectileSpeed * Mathf.Max(0.1f, speedMultiplier));
            float maxDistance = Mathf.Max(1f, levelData.range * Mathf.Max(0.1f, areaMultiplier));
            float hitRadius = Mathf.Max(0.1f, levelData.projectileHitRadius);
            float flightTime = maxDistance / speed;

            Vector3 farPoint = origin + direction * maxDistance;
            var hitOutbound = new HashSet<ulong>();
            var hitReturn = new HashSet<ulong>();

            // 왕복 연출: 각 클라이언트가 로컬로 왕복 궤적을 보간해 표시(서버가 매 프레임 위치를 보내지 않음)
            vfx?.ShowGrenade(origin, farPoint, 0f, flightTime);

            Vector3 pos = origin;
            float traveled = 0f;
            int stackCount = 0;

            while (traveled < maxDistance)
            {
                float step = speed * Time.deltaTime;
                traveled += step;
                pos += direction * step;

                stackCount += DamageNewTargets(pos, hitRadius, baseDamage, ownerClientId, skillTag, hitOutbound);
                yield return null;
            }

            float returnDamage = baseDamage * (1f + stackCount * Mathf.Max(0f, levelData.boomerangDamageAmplifyPerStack));
            // ShowGrenade는 고정된 두 지점 사이를 보간하는 연출이라 실시간으로 움직이는 플레이어를
            // 쫓아가는 모습을 표현할 수 없다 — 대신 시전자를 매 프레임 실시간으로 추적하는 전용 연출을 사용한다.
            vfx?.ShowHomingReturn(farPoint, speed);

            // 부메랑처럼 시간제한 없이 플레이어와 완전히 부딪힐 때까지 계속 따라간다.
            // 플레이어가 죽거나 씬을 떠나면 casterTransform이 파괴되어 폴백 위치로 향하다 도달해 종료된다.
            while (true)
            {
                Vector3 returnTarget = GetCasterReturnPosition(casterTransform, origin);
                Vector3 toTarget = returnTarget - pos;
                toTarget.y = 0f;

                float distance = toTarget.magnitude;
                if (distance <= hitRadius) break;

                float step = Mathf.Min(speed * Time.deltaTime, distance);
                pos += toTarget.normalized * step;

                DamageNewTargets(pos, hitRadius, returnDamage, ownerClientId, skillTag, hitReturn);
                yield return null;
            }
        }

        private static Vector3 GetCasterReturnPosition(Transform casterTransform, Vector3 fallbackOrigin)
        {
            return casterTransform != null
                ? casterTransform.position + Vector3.up * SpawnHeightOffset
                : fallbackOrigin;
        }

        private static int DamageNewTargets(
            Vector3 position, float radius, float damage, ulong ownerClientId, string skillTag, HashSet<ulong> hitIds)
        {
            int newHits = 0;
            int overlapCount = Physics.OverlapSphereNonAlloc(position, radius, OverlapBuffer);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null || !col.TryGetComponent<EnemyNetworkBase>(out var enemy))
                {
                    OverlapBuffer[i] = null;
                    continue;
                }

                if (hitIds.Add(enemy.NetworkObjectId))
                {
                    enemy.TakeDamage(damage, ownerClientId, skillTag);
                    newHits++;
                }

                OverlapBuffer[i] = null;
            }
            return newHits;
        }
    }
}
