using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;

namespace Vamsurlike.Skills
{
    public class UltimateNetworkSkill : SkillBase
    {
        private const float DefaultSpawnHeight = 0.8f;

        protected override SkillCastType SupportedCastType => SkillCastType.Ultimate;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            if (skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[{nameof(UltimateNetworkSkill)}] projectilePrefab is not assigned. skill={skill.name}");
                return false;
            }

            // in 파라미터는 코루틴에 캡처 불가 — 필요한 값만 추출
            StartCoroutine(FireWavesCoroutine(
                skill.projectilePrefab,
                levelData,
                context.FinalDamage,
                context.OwnerClientId,
                context.CasterTransform,
                skill.name));

            return true;
        }

        private IEnumerator FireWavesCoroutine(
            GameObject projectilePrefab,
            SkillLevelData levelData,
            float finalDamage,
            ulong ownerClientId,
            Transform casterTransform,
            string skillName)
        {
            int   waveCount      = Mathf.Max(1, levelData.waveCount);
            int   bulletsPerWave = Mathf.Max(1, levelData.projectileCount);
            float waveDelay      = Mathf.Max(0f, levelData.waveDelay);
            float rotPerWave     = levelData.rotationPerWave;
            float angleStep      = 360f / bulletsPerWave;

            Debug.Log($"[{nameof(UltimateNetworkSkill)}] BulletStorm start. skill={skillName}, waves={waveCount}, bullets/wave={bulletsPerWave}, delay={waveDelay}s, damage={finalDamage}");

            for (int wave = 0; wave < waveCount; wave++)
            {
                float waveAngle = rotPerWave * wave;

                for (int i = 0; i < bulletsPerWave; i++)
                {
                    if (casterTransform == null) 
                        yield break;

                    // 매 총알마다 origin 갱신 — 발사 중 이동 시에도 추적
                    Vector3 origin = casterTransform.position + Vector3.up * DefaultSpawnHeight;
                    float   angle  = angleStep * i + waveAngle;
                    Vector3 dir    = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
                    SpawnBullet(projectilePrefab, levelData, finalDamage, ownerClientId, origin, dir);

                    // 마지막 총알 이후에는 대기 없음
                    bool isLast = (wave == waveCount - 1) && (i == bulletsPerWave - 1);
                    if (!isLast && waveDelay > 0f)
                        yield return new WaitForSeconds(waveDelay);
                }
            }

            if (casterTransform != null)
                PlayUltimateVFXClientRpc(casterTransform.position);

            Debug.Log($"[{nameof(UltimateNetworkSkill)}] BulletStorm complete. skill={skillName}");
        }

        private void SpawnBullet(
            GameObject prefab,
            SkillLevelData levelData,
            float finalDamage,
            ulong ownerClientId,
            Vector3 position,
            Vector3 direction)
        {
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

            if (prefab.TryGetComponent<NetworkProjectile>(out var projTemplate))
                rotation = projTemplate.GetProjectileRotation(direction);

            NetworkObject obj = PoolManager.Instance != null
                ? PoolManager.Instance.GetNetworkObject(prefab, position, rotation)
                : Instantiate(prefab, position, rotation).GetComponent<NetworkObject>();

            if (obj == null) return;

            if (obj.TryGetComponent<NetworkProjectile>(out var projectile))
                projectile.Initialize(prefab, ownerClientId, position, direction, levelData, finalDamage);
            else
                Debug.LogWarning($"[{nameof(UltimateNetworkSkill)}] Spawned object has no NetworkProjectile. prefab={prefab.name}");

            obj.Spawn(true);
        }

        [ClientRpc]
        private void PlayUltimateVFXClientRpc(Vector3 position)
        {
            // Phase 8: 궁극기 완료 VFX 연결
        }
    }
}
