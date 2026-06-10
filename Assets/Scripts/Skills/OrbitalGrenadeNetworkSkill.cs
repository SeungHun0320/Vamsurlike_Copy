using System.Collections;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 합체 스킬: Orbital + Grenade.
    // 위성이 궤도를 돌다가 틱마다 착지 폭발. 위성 수만큼 동시 폭발.
    public sealed class OrbitalGrenadeSkill : OrbitalVisualBase
    {
        private const float DropArcHeight  = 1.5f;
        private const float DropFlightTime = 0.3f;
        private const float HeightOffset   = 0.9f;

        private bool  broadcastSent;
        private int   cachedCount;
        private float cachedRadius;
        private float cachedRotSpeed;

        public OrbitalGrenadeSkill(GameObject visualPrefab, float heightOffset)
            : base(visualPrefab, heightOffset) { }

        public override SkillCastType SupportedCastType => SkillCastType.OrbitalGrenade;
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
                context.Manager.BroadcastOrbitalGrenadeClientRpc(count, radius, rotSpeed);
            }

            Vector3 origin = context.CasterTransform.position;

            for (int i = 0; i < count; i++)
            {
                Vector3 orbPos    = GetOrbitalPosition(origin + Vector3.up * HeightOffset, radius, rotSpeed, i, count);
                Vector3 groundPos = new Vector3(orbPos.x, origin.y + 0.1f, orbPos.z);

                context.Manager.BroadcastGrenadeVFXClientRpc(orbPos, groundPos, DropArcHeight, DropFlightTime);
                context.Manager.StartSkillCoroutine(
                    ExplosionCoroutine(groundPos, levelData.splashRadius, context.FinalDamage));
            }

            Debug.Log($"[{nameof(OrbitalGrenadeSkill)}] 폭발 틱. orbCount={count}, damage={context.FinalDamage}, splash={levelData.splashRadius}");
            return true;
        }

        private static IEnumerator ExplosionCoroutine(Vector3 pos, float radius, float damage)
        {
            float elapsed = 0f;
            while (elapsed < DropFlightTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            var cols = Physics.OverlapSphere(pos, radius);
            foreach (var col in cols)
                if (col.TryGetComponent<EnemyNetworkBase>(out var e))
                    e.TakeDamage(damage);
        }
    }
}
