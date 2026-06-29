using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;
using Vamsurlike.Network;

namespace Vamsurlike.Skills
{
    // 산탄 — 한 발씩 연속 발사. 반동이 누적되어 탄착군이 퍼짐.
    public class ScatterShotSkill : SkillBase
    {
        private const float DefaultSpawnHeight = 0.8f;

        // RULES.md: 시드 기반 System.Random 사용
        private readonly System.Random rng = new();

        // 연속 발사 중 중복 burst 방지
        private bool isBurstActive;

        public override SkillCastType SupportedCastType => SkillCastType.ScatterShot;

        // 쿨다운 방식으로 변경 — SkillManager의 UpdateCooldownSkill이 처리
        public override bool IsPersistentExecution => false;

        protected override bool Execute(in SkillCastContext context, Vector3 direction, EnemyNetworkBase _)
        {
            if (isBurstActive) return false; // burst 중 중복 시작 차단

            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(ScatterShotSkill)}] projectilePrefab 미할당. skill={skill.name}");
                return false;
            }

            isBurstActive = true;
            context.CoroutineRunner.StartSkillCoroutine(FireBurstCoroutine(
                skill.projectilePrefab, levelData, context.FinalDamage,
                context.OwnerClientId, context.CasterTransform, direction, skill.name, context.SpeedMultiplier));

            return true;
        }

        private IEnumerator FireBurstCoroutine(
            GameObject prefab, SkillLevelData levelData, float finalDamage,
            ulong ownerClientId, Transform casterTransform, Vector3 baseForward, string skillName,
            float speedMultiplier = 1f)
        {
            int count = Mathf.Max(1, levelData.scatterBulletCount);
            float halfSpread = levelData.scatterAngle * 0.5f;
            // 샷당 반동 강도: 총 각도를 총알 수로 나눈 값의 2배 → 누적 시 전체 부채꼴에 걸쳐 분산
            float kickRange = levelData.scatterAngle / count * 2f;
            float interval = count > 1 ? levelData.burstDuration / (count - 1) : 0f;

            float recoilAngle = 0f; // 누적 반동 각도

            for (int i = 0; i < count; i++)
            {
                if (casterTransform == null) break;

                // 반동 누적: 이전 탄 방향에서 랜덤하게 튀는 효과
                float kick = (float)(rng.NextDouble() * kickRange - kickRange * 0.5f);
                recoilAngle = Mathf.Clamp(recoilAngle + kick, -halfSpread, halfSpread);

                Vector3 origin = casterTransform.position + Vector3.up * DefaultSpawnHeight;
                Vector3 dir = Quaternion.AngleAxis(recoilAngle, Vector3.up) * baseForward;
                SpawnBullet(prefab, levelData, finalDamage, ownerClientId, origin, dir, speedMultiplier);

                if (i < count - 1 && interval > 0f)
                    yield return new WaitForSeconds(interval);
            }

            isBurstActive = false;
        }

        private static void SpawnBullet(GameObject prefab, SkillLevelData levelData, float finalDamage,
            ulong ownerClientId, Vector3 position, Vector3 direction, float speedMultiplier = 1f)
        {
            Quaternion rot = Quaternion.LookRotation(direction, Vector3.up);
            if (prefab.TryGetComponent<NetworkProjectile>(out var template))
                rot = template.GetProjectileRotation(direction);

            NetworkObject obj = PoolManager.GetOrInstantiateNetworkObject(
                prefab,
                position,
                rot,
                nameof(ScatterShotSkill));

            if (obj == null) return;

            if (obj.TryGetComponent<NetworkProjectile>(out var projectile))
                projectile.Initialize(prefab, ownerClientId, position, direction, levelData, finalDamage, speedMultiplier);
            else
                Debug.LogWarning($"[{nameof(ScatterShotSkill)}] NetworkProjectile 없음. prefab={prefab.name}");

            obj.Spawn(true);
        }
    }
}
