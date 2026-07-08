using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Items;
using Vamsurlike.Network;
using Vamsurlike.Upgrades;
using Vamsurlike.VFX;

namespace Vamsurlike.Skills
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class SkillVFXController : NetworkBehaviour, ISkillVFXBroadcaster
    {
        private const float MeleeVisualDuration = 0.5f;
        private const float ParabolaHeightMultiplier = 4f;
        private const float TelegraphHeightOffset = 0.05f;

        private static readonly Color AuraColor = new(0.15f, 0.85f, 1f, 0.75f);
        private static readonly Color BlackHoleColor = new(0.45f, 0.1f, 1f, 0.9f);
        private static readonly Color GrenadeImpactColor = new(1f, 0.55f, 0f, 0.85f);

        [SerializeField] private CharacterDataSO characterData;
        [SerializeField] private GameObject orbitalVisualPrefab;
        [SerializeField] private float orbitalHeightOffset = 0.9f;
        [SerializeField] private float meleeVisualForwardOffsetRatio = 0.5f;
        [SerializeField] private float meleeVisualHeightOffset = 2.2f;
        [SerializeField] private GameObject meleeArcPrefab;
        [SerializeField] private GameObject meleeBoxPrefab;
        [SerializeField] private GameObject grenadeImpactCirclePrefab;

        [Header("Ultimate VFX")]
        [SerializeField] private VFXSpawnEventSO vfxSpawnEvent;
        [SerializeField] private float ultimateVFXDuration = 0.5f;
        [SerializeField] private float grenadeExplosionVFXDuration = 0.5f;

        private readonly Dictionary<SkillCastType, GameObject> vfxPrefabsByType = new();
        private readonly Dictionary<SkillCastType, AreaCircleVFX> areaCircleVfxByType = new();

        private GameObject[] orbitalVisualObjects;
        private float orbitalRadius;
        private float orbitalRotationSpeed;

        public override void OnNetworkSpawn()
        {
            if (!IsClient) return;
            BuildVFXPrefabMap();
        }

        public override void OnNetworkDespawn()
        {
            DestroyAllVisuals();
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsClient || orbitalVisualObjects == null) return;

            Vector3 origin = transform.position + Vector3.up * orbitalHeightOffset;
            for (int i = 0; i < orbitalVisualObjects.Length; i++)
            {
                GameObject visual = orbitalVisualObjects[i];
                if (visual == null) continue;
                visual.transform.position = OrbitalPositionMath.Calculate(
                    origin,
                    orbitalRadius,
                    orbitalRotationSpeed,
                    i,
                    orbitalVisualObjects.Length);
            }
        }

        public void ShowOrbital(int count, float radius, float rotationSpeed)
        {
            if (!IsServer) return;
            ShowOrbitalClientRpc(count, radius, rotationSpeed);
        }

        public void ShowBlackHole(Vector3 center, float duration, float radius)
        {
            if (!IsServer) return;
            ShowBlackHoleClientRpc(center, duration, radius);
        }

        public void ShowAreaCircle(SkillCastType castType, float radius, float duration, bool followOwner)
        {
            if (!IsServer) return;
            ShowAreaCircleClientRpc(castType, radius, duration, followOwner);
        }

        public void ShowUltimate(Vector3 position)
        {
            if (!IsServer) return;
            ShowUltimateClientRpc(position);
        }

        public void ShowMelee(SkillCastType castType, Vector3 position, Vector3 forward, float range, float arcAngle)
        {
            if (!IsServer) return;
            ShowMeleeClientRpc(castType, position, forward, range, arcAngle);
        }

        public void ShowGrenade(Vector3 from, Vector3 to, float arcHeight, float flightTime)
        {
            if (!IsServer) return;
            ShowGrenadeClientRpc(from, to, arcHeight, flightTime);
        }

        public void ShowGrenadeImpactCircle(Vector3 center, float radius, float duration)
        {
            if (!IsServer) return;
            ShowGrenadeImpactCircleClientRpc(center, radius, duration);
        }

        public void ShowHomingReturn(Vector3 from, float speed)
        {
            if (!IsServer) return;
            ShowHomingReturnClientRpc(from, speed);
        }

        public void ShowMeleeBox(SkillCastType castType, Vector3 position, Vector3 forward, float range, float width)
        {
            if (!IsServer) return;
            ShowMeleeBoxClientRpc(castType, position, forward, range, width);
        }

        public void RemoveSkillVisual(SkillCastType castType)
        {
            if (!IsServer) return;
            RemoveSkillVisualClientRpc(castType);
        }

        [ClientRpc]
        private void ShowOrbitalClientRpc(int count, float radius, float rotationSpeed)
        {
            if (orbitalVisualPrefab == null)
            {
                Debug.LogWarning($"[{nameof(SkillVFXController)}] orbitalVisualPrefab이 없습니다.", this);
                return;
            }

            DestroyOrbitalVisuals();
            int safeCount = Mathf.Max(1, count);
            orbitalRadius = radius;
            orbitalRotationSpeed = rotationSpeed;
            orbitalVisualObjects = new GameObject[safeCount];

            Vector3 origin = transform.position + Vector3.up * orbitalHeightOffset;
            for (int i = 0; i < safeCount; i++)
            {
                Vector3 position = OrbitalPositionMath.Calculate(
                    origin,
                    radius,
                    rotationSpeed,
                    i,
                    safeCount);
                orbitalVisualObjects[i] = PoolManager.GetOrInstantiateGO(
                    orbitalVisualPrefab, position, Quaternion.identity, nameof(SkillVFXController));
            }
        }

        [ClientRpc]
        private void ShowBlackHoleClientRpc(Vector3 center, float duration, float radius)
        {
            SpawnAreaCircleVFX(
                SkillCastType.BlackHole,
                center,
                radius,
                duration,
                BlackHoleColor);
        }

        [ClientRpc]
        private void ShowAreaCircleClientRpc(
            SkillCastType castType,
            float radius,
            float duration,
            bool followOwner)
        {
            Transform followTarget = followOwner ? transform : null;
            Vector3 position = followTarget != null ? followTarget.position : transform.position;
            Color color = castType == SkillCastType.AreaAura ? AuraColor : BlackHoleColor;
            SpawnAreaCircleVFX(castType, position, radius, duration, color, followTarget);
        }

        [ClientRpc]
        private void ShowUltimateClientRpc(Vector3 position)
        {
            vfxSpawnEvent?.Raise(new VFXCue(VFXCueIds.Ultimate, position, Vector3.up, 1f, ultimateVFXDuration, Color.white));
        }

        [ClientRpc]
        private void ShowMeleeClientRpc(SkillCastType castType, Vector3 position, Vector3 forward, float range, float arcAngle)
        {
            Vector3 flatForward = forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = transform.forward;
            flatForward.Normalize();

            // 부채꼴 범위 표시
            MeleeArcVFX.Spawn(meleeArcPrefab, position, flatForward, range, arcAngle * 0.5f, MeleeVisualDuration);

            // 무기 VFX — 호출한 스킬 자신의 castType으로 조회한다 (예전엔 SkillCastType.Melee로
            // 고정되어 있어 샷건/관통산탄 등 원뿔 즉발 스킬에도 망치 모델이 잘못 표시됐다).
            if (!TryGetVFXPrefab(castType, out GameObject prefab)) return;

            GameObject visual = SpawnPooledVisual(
                prefab,
                position
                + flatForward * range * meleeVisualForwardOffsetRatio
                + Vector3.up * meleeVisualHeightOffset,
                Quaternion.LookRotation(flatForward, Vector3.up));
            StartCoroutine(ReturnVisualAfterDelay(prefab, visual, MeleeVisualDuration));
        }

        [ClientRpc]
        private void ShowGrenadeClientRpc(
            Vector3 from,
            Vector3 to,
            float arcHeight,
            float flightTime)
        {
            vfxPrefabsByType.TryGetValue(SkillCastType.Grenade, out GameObject prefab);
            StartCoroutine(GrenadeVisualCoroutine(from, to, arcHeight, flightTime, prefab));
        }

        [ClientRpc]
        private void ShowHomingReturnClientRpc(Vector3 from, float speed)
        {
            vfxPrefabsByType.TryGetValue(SkillCastType.Grenade, out GameObject prefab);
            StartCoroutine(HomingReturnVisualCoroutine(from, speed, prefab));
        }

        // 왕복 관통창 귀환 연출 — 고정 목적지가 아니라 이 컴포넌트의 소유자(시전자) 실시간 위치를
        // 매 프레임 다시 추적한다. transform은 Netcode로 이미 동기화된 소유자 트랜스폼이라 별도
        // 위치 갱신 RPC 없이도 모든 클라이언트에서 정확히 같은 지점을 쫓아간다.
        private IEnumerator HomingReturnVisualCoroutine(Vector3 from, float speed, GameObject prefab)
        {
            const float ArriveThreshold = 0.3f;

            GameObject visual = prefab != null
                ? SpawnPooledVisual(prefab, from, Quaternion.identity)
                : null;

            Vector3 pos = from;
            while (true)
            {
                Vector3 toTarget = transform.position - pos;
                float distance = toTarget.magnitude;
                if (distance <= ArriveThreshold) break;

                float step = Mathf.Min(Mathf.Max(0.1f, speed) * Time.deltaTime, distance);
                pos += toTarget.normalized * step;
                if (visual != null) visual.transform.position = pos;
                yield return null;
            }

            ReturnVisual(prefab, visual);
        }

        [ClientRpc]
        private void ShowMeleeBoxClientRpc(SkillCastType castType, Vector3 position, Vector3 forward, float range, float width)
        {
            Vector3 flatForward = forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = transform.forward;
            flatForward.Normalize();

            // 사각형 범위 표시
            MeleeBoxVFX.Spawn(meleeBoxPrefab, position, flatForward, range, width, MeleeVisualDuration);

            // 무기 VFX — 호출한 스킬 자신의 castType으로 조회
            if (!TryGetVFXPrefab(castType, out GameObject prefab)) return;

            GameObject visual = SpawnPooledVisual(
                prefab,
                position
                + flatForward * range * meleeVisualForwardOffsetRatio
                + Vector3.up * meleeVisualHeightOffset,
                Quaternion.LookRotation(flatForward, Vector3.up));
            StartCoroutine(ReturnVisualAfterDelay(prefab, visual, MeleeVisualDuration));
        }

        [ClientRpc]
        private void ShowGrenadeImpactCircleClientRpc(Vector3 center, float radius, float duration)
        {
            if (grenadeImpactCirclePrefab == null)
            {
                Debug.LogWarning($"[{nameof(SkillVFXController)}] grenadeImpactCirclePrefab이 없습니다.", this);
                return;
            }

            Vector3 position = center + Vector3.up * TelegraphHeightOffset;
            GameObject visual = PoolManager.GetOrInstantiateGO(
                grenadeImpactCirclePrefab, position, Quaternion.identity, nameof(SkillVFXController));
            if (visual == null) return;

            if (!visual.TryGetComponent(out AreaCircleVFX circle))
            {
                Debug.LogWarning(
                    $"[{nameof(SkillVFXController)}] AreaCircleVFX 컴포넌트가 없습니다. prefab={grenadeImpactCirclePrefab.name}",
                    this);
                PoolManager.ReturnOrDestroyGO(grenadeImpactCirclePrefab, visual, nameof(SkillVFXController));
                return;
            }

            circle.Initialize(radius, duration, GrenadeImpactColor, null, grenadeImpactCirclePrefab);
        }

        [ClientRpc]
        private void RemoveSkillVisualClientRpc(SkillCastType castType)
        {
            if (castType == SkillCastType.Orbital)
                DestroyOrbitalVisuals();
            DestroyAreaCircleVFX(castType);
        }

        private void BuildVFXPrefabMap()
        {
            vfxPrefabsByType.Clear();
            AddSkillPrefabs(characterData != null ? characterData.startingSkills : null);

            UpgradeCatalog upgradeCatalog = UpgradeCatalog.Instance;
            if (upgradeCatalog != null && upgradeCatalog.options != null)
            {
                for (int i = 0; i < upgradeCatalog.options.Length; i++)
                {
                    UpgradeOptionSO option = upgradeCatalog.options[i];
                    AddSkillPrefab(option != null ? option.skillData : null);
                }
            }

            CombineRecipeCatalog recipeCatalog = CombineRecipeCatalog.Instance;
            if (recipeCatalog == null || recipeCatalog.recipes == null) return;

            for (int i = 0; i < recipeCatalog.recipes.Length; i++)
            {
                CombineRecipeSO recipe = recipeCatalog.recipes[i];
                AddSkillPrefab(recipe != null ? recipe.evolvedSkill : null);
            }
        }

        private void AddSkillPrefabs(SkillDataSO[] skills)
        {
            if (skills == null) return;
            for (int i = 0; i < skills.Length; i++)
                AddSkillPrefab(skills[i]);
        }

        private void AddSkillPrefab(SkillDataSO skill)
        {
            if (skill == null || skill.vfxPrefab == null) return;
            vfxPrefabsByType[skill.castType] = skill.vfxPrefab;
        }

        private bool TryGetVFXPrefab(SkillCastType castType, out GameObject prefab)
        {
            if (vfxPrefabsByType.TryGetValue(castType, out prefab) && prefab != null)
                return true;

            BuildVFXPrefabMap();
            return vfxPrefabsByType.TryGetValue(castType, out prefab) && prefab != null;
        }

        private IEnumerator GrenadeVisualCoroutine(
            Vector3 from,
            Vector3 to,
            float arcHeight,
            float flightTime,
            GameObject prefab)
        {
            GameObject visual = prefab != null
                ? SpawnPooledVisual(prefab, from, Quaternion.identity)
                : null;

            float safeFlightTime = Mathf.Max(Mathf.Epsilon, flightTime);
            float elapsed = 0f;
            while (elapsed < safeFlightTime)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / safeFlightTime);
                Vector3 position = Vector3.Lerp(from, to, progress);
                position.y += arcHeight
                    * ParabolaHeightMultiplier
                    * progress
                    * (1f - progress);
                if (visual != null) visual.transform.position = position;
                yield return null;
            }

            ReturnVisual(prefab, visual);
            vfxSpawnEvent?.Raise(new VFXCue(
                VFXCueIds.GrenadeExplosion, to, Vector3.up, 1f, grenadeExplosionVFXDuration, Color.white));
        }

        private static GameObject SpawnPooledVisual(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return PoolManager.Instance != null
                ? PoolManager.Instance.GetGO(prefab, position, rotation)
                : Instantiate(prefab, position, rotation);
        }

        private static IEnumerator ReturnVisualAfterDelay(GameObject prefab, GameObject visual, float delay)
        {
            if (visual == null) yield break;
            yield return new WaitForSeconds(Mathf.Max(0f, delay));
            ReturnVisual(prefab, visual);
        }

        private static void ReturnVisual(GameObject prefab, GameObject visual)
        {
            if (visual == null) return;
            if (prefab != null && PoolManager.Instance != null)
            {
                PoolManager.Instance.ReturnGO(prefab, visual);
                return;
            }

            Destroy(visual);
        }

        private void SpawnAreaCircleVFX(
            SkillCastType castType,
            Vector3 position,
            float radius,
            float duration,
            Color color,
            Transform followTarget = null)
        {
            if (!vfxPrefabsByType.TryGetValue(castType, out GameObject prefab) || prefab == null)
                return;

            if (duration <= 0f)
                DestroyAreaCircleVFX(castType);

            GameObject instance = PoolManager.GetOrInstantiateGO(
                prefab, position, Quaternion.identity, nameof(SkillVFXController));
            if (instance == null) return;

            if (!instance.TryGetComponent(out AreaCircleVFX vfx))
            {
                Debug.LogWarning(
                    $"[{nameof(SkillVFXController)}] AreaCircleVFX 컴포넌트가 없습니다. prefab={prefab.name}",
                    this);
                PoolManager.ReturnOrDestroyGO(prefab, instance, nameof(SkillVFXController));
                return;
            }

            vfx.Initialize(radius, duration, color, followTarget, prefab);
            if (duration <= 0f)
                areaCircleVfxByType[castType] = vfx;
        }

        private void DestroyAllVisuals()
        {
            DestroyOrbitalVisuals();
            foreach (SkillCastType castType in new List<SkillCastType>(areaCircleVfxByType.Keys))
                DestroyAreaCircleVFX(castType);
            areaCircleVfxByType.Clear();
        }

        private void DestroyOrbitalVisuals()
        {
            if (orbitalVisualObjects == null) return;

            for (int i = 0; i < orbitalVisualObjects.Length; i++)
            {
                if (orbitalVisualObjects[i] != null)
                    PoolManager.ReturnOrDestroyGO(orbitalVisualPrefab, orbitalVisualObjects[i], nameof(SkillVFXController));
            }
            orbitalVisualObjects = null;
        }

        private void DestroyAreaCircleVFX(SkillCastType castType)
        {
            if (!areaCircleVfxByType.TryGetValue(castType, out AreaCircleVFX vfx)) return;
            areaCircleVfxByType.Remove(castType);
            if (vfx == null) return;

            GameObject prefab = vfxPrefabsByType.TryGetValue(castType, out GameObject p) ? p : null;
            PoolManager.ReturnOrDestroyGO(prefab, vfx.gameObject, nameof(SkillVFXController));
        }
    }
}
