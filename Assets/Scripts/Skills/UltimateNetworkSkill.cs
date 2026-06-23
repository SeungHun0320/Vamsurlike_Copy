using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;

namespace Vamsurlike.Skills
{
    public sealed class UltimateSkill : SkillBase
    {
        private const float DefaultSpawnHeight = 0.8f;

        public override SkillCastType SupportedCastType => SkillCastType.Ultimate;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null
                || levelData == null
                || context.CasterTransform == null
                || context.CoroutineRunner == null)
                return false;

            if (skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(UltimateSkill)}] projectilePrefab 미할당. skill={skill.name}");
                return false;
            }

            // in 파라미터는 코루틴에 캡처 불가 — 필요한 값만 추출
            context.CoroutineRunner.StartSkillCoroutine(FireWavesCoroutine(
                skill.projectilePrefab, levelData, context.FinalDamage,
                context.OwnerClientId, context.CasterTransform, skill.name, context.VFX));

            return true;
        }

        private static IEnumerator FireWavesCoroutine(
            GameObject prefab, SkillLevelData levelData, float finalDamage,
            ulong ownerClientId,
            Transform casterTransform,
            string skillName,
            ISkillVFXBroadcaster vfx)
        {
            int waveCount = Mathf.Max(1, levelData.waveCount);
            int bulletsPerWave = Mathf.Max(1, levelData.projectileCount);
            float waveDelay = Mathf.Max(0f, levelData.waveDelay);
            float rotPerWave = levelData.rotationPerWave;
            float angleStep = 360f / bulletsPerWave;

            Debug.Log($"[{nameof(UltimateSkill)}] BulletStorm 시작. skill={skillName}, waves={waveCount}, bullets/wave={bulletsPerWave}, delay={waveDelay}s, damage={finalDamage}");

            for (int wave = 0; wave < waveCount; wave++)
            {
                float waveAngle = rotPerWave * wave;
                for (int i = 0; i < bulletsPerWave; i++)
                {
                    if (casterTransform == null) yield break;

                    Vector3 origin = casterTransform.position + Vector3.up * DefaultSpawnHeight;
                    float angle = angleStep * i + waveAngle;
                    Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
                    SpawnBullet(prefab, levelData, finalDamage, ownerClientId, origin, dir);

                    bool isLast = (wave == waveCount - 1) && (i == bulletsPerWave - 1);
                    if (!isLast && waveDelay > 0f)
                        yield return new WaitForSeconds(waveDelay);
                }
            }

            if (casterTransform != null)
                vfx?.ShowUltimate(casterTransform.position);

            Debug.Log($"[{nameof(UltimateSkill)}] BulletStorm 완료. skill={skillName}");
        }

        private static void SpawnBullet(GameObject prefab, SkillLevelData levelData, float finalDamage,
            ulong ownerClientId, Vector3 position, Vector3 direction)
        {
            Quaternion rot = Quaternion.LookRotation(direction, Vector3.up);
            if (prefab.TryGetComponent<NetworkProjectile>(out var template))
                rot = template.GetProjectileRotation(direction);

            NetworkObject obj = PoolManager.Instance != null
                ? PoolManager.Instance.GetNetworkObject(prefab, position, rot)
                : Object.Instantiate(prefab, position, rot).GetComponent<NetworkObject>();

            if (obj == null) return;

            if (obj.TryGetComponent<NetworkProjectile>(out var projectile))
                projectile.Initialize(prefab, ownerClientId, position, direction, levelData, finalDamage);
            else
                Debug.LogWarning($"[{nameof(UltimateSkill)}] NetworkProjectile 없음. prefab={prefab.name}");

            obj.Spawn(true);
        }
    }
}
