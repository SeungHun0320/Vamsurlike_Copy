using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 합체 스킬: DamageAura + Orbital.
    // 범위 내 적을 플레이어 방향으로 끌어당기고, 위성이 접촉한 적에게 데미지.
    public sealed class BlackHoleSkill : OrbitalVisualBase
    {
        private const float HeightOffset = 0.9f;

        private bool  broadcastSent;
        private int   cachedCount;
        private float cachedRadius;
        private float cachedRotSpeed;

        private readonly List<EnemyNetworkBase> pullTargets = new();
        private readonly HashSet<ulong>         hitEnemyIds = new();

        public BlackHoleSkill(GameObject visualPrefab, float heightOffset)
            : base(visualPrefab, heightOffset) { }

        public override SkillCastType SupportedCastType => SkillCastType.BlackHole;
        public override bool IsPersistentExecution => true;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillLevelData levelData = context.LevelData;
            if (context.CasterTransform == null || levelData == null) return false;

            int   count    = Mathf.Max(1, levelData.orbitalCount);
            float radius   = Mathf.Max(0.1f, levelData.orbitalRadius);
            float rotSpeed = levelData.orbitalRotationSpeed;

            if (!broadcastSent
                || cachedCount != count
                || !Mathf.Approximately(cachedRadius,   radius)
                || !Mathf.Approximately(cachedRotSpeed, rotSpeed))
            {
                broadcastSent  = true;
                cachedCount    = count;
                cachedRadius   = radius;
                cachedRotSpeed = rotSpeed;
                context.Manager.BroadcastBlackHoleClientRpc(count, radius, rotSpeed);
            }

            Vector3 playerPos  = context.CasterTransform.position;
            float   pullRadius = levelData.areaRadius > 0f ? levelData.areaRadius : levelData.range;
            float   pullDist   = levelData.pullSpeed * levelData.tickInterval;

            // 범위 내 적 끌어당김
            int pullCount = AutoTargeting.FindEnemiesInRange(playerPos, pullRadius, pullTargets);
            for (int i = 0; i < pullCount; i++)
            {
                var enemy = pullTargets[i];
                if (enemy == null) continue;

                var ai = enemy.GetComponent<EnemyAI>();
                if (ai?.Agent == null || !ai.Agent.enabled || !ai.Agent.isOnNavMesh) continue;

                Vector3 dir  = playerPos - enemy.transform.position;
                dir.y = 0f;
                if (dir.magnitude > 0.5f)
                    ai.Agent.Move(dir.normalized * pullDist);
            }

            // 위성 접촉 데미지
            hitEnemyIds.Clear();
            int damagedCount = 0;

            for (int i = 0; i < count; i++)
            {
                Vector3 orbPos = GetOrbitalPosition(playerPos + Vector3.up * HeightOffset, radius, rotSpeed, i, count);
                var cols = Physics.OverlapSphere(orbPos, Mathf.Max(0.05f, levelData.orbitalHitRadius));

                foreach (var col in cols)
                {
                    if (!col.TryGetComponent<EnemyNetworkBase>(out var e)) continue;
                    if (!hitEnemyIds.Add(e.NetworkObjectId)) continue;
                    e.TakeDamage(context.FinalDamage);
                    damagedCount++;
                }
            }

            if (pullCount == 0 && damagedCount == 0)
            {
                if (ShouldLogNoTarget())
                    Debug.Log($"[{nameof(BlackHoleSkill)}] 범위 내 적 없음. pullRadius={pullRadius}, orbRadius={radius}");
                return false;
            }

            Debug.Log($"[{nameof(BlackHoleSkill)}] 틱. pulled={pullCount}, damaged={damagedCount}, damage={context.FinalDamage}");
            return true;
        }
    }
}
