using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Items;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Skills
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class SkillVFXController : NetworkBehaviour, ISkillVFXBroadcaster
    {
        private const float MeleeVisualDuration = 0.5f;
        private const float ParabolaHeightMultiplier = 4f;

        private static readonly Color AuraColor = new(0.15f, 0.85f, 1f, 0.75f);
        private static readonly Color BlackHoleColor = new(0.45f, 0.1f, 1f, 0.9f);

        [SerializeField] private CharacterDataSO characterData;
        [SerializeField] private GameObject orbitalVisualPrefab;
        [SerializeField] private float orbitalHeightOffset = 0.9f;

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

        public void ShowMelee(Vector3 position, Vector3 forward)
        {
            if (!IsServer) return;
            ShowMeleeClientRpc(position, forward);
        }

        public void ShowGrenade(Vector3 from, Vector3 to, float arcHeight, float flightTime)
        {
            if (!IsServer) return;
            ShowGrenadeClientRpc(from, to, arcHeight, flightTime);
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
                orbitalVisualObjects[i] = Instantiate(
                    orbitalVisualPrefab,
                    position,
                    Quaternion.identity);
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
            _ = position;
        }

        [ClientRpc]
        private void ShowMeleeClientRpc(Vector3 position, Vector3 forward)
        {
            if (!vfxPrefabsByType.TryGetValue(SkillCastType.Melee, out GameObject prefab)
                || prefab == null)
                return;

            GameObject visual = Instantiate(
                prefab,
                position,
                Quaternion.LookRotation(forward, Vector3.up));
            Destroy(visual, MeleeVisualDuration);
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

        private IEnumerator GrenadeVisualCoroutine(
            Vector3 from,
            Vector3 to,
            float arcHeight,
            float flightTime,
            GameObject prefab)
        {
            GameObject visual = prefab != null
                ? Instantiate(prefab, from, Quaternion.identity)
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

            if (visual != null) Destroy(visual);
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

            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            if (!instance.TryGetComponent(out AreaCircleVFX vfx))
            {
                Debug.LogWarning(
                    $"[{nameof(SkillVFXController)}] AreaCircleVFX 컴포넌트가 없습니다. prefab={prefab.name}",
                    this);
                Destroy(instance);
                return;
            }

            vfx.Initialize(radius, duration, color, followTarget);
            if (duration <= 0f)
                areaCircleVfxByType[castType] = vfx;
        }

        private void DestroyAllVisuals()
        {
            DestroyOrbitalVisuals();
            foreach (KeyValuePair<SkillCastType, AreaCircleVFX> entry in areaCircleVfxByType)
            {
                if (entry.Value != null)
                    Destroy(entry.Value.gameObject);
            }
            areaCircleVfxByType.Clear();
        }

        private void DestroyOrbitalVisuals()
        {
            if (orbitalVisualObjects == null) return;

            for (int i = 0; i < orbitalVisualObjects.Length; i++)
            {
                if (orbitalVisualObjects[i] != null)
                    Destroy(orbitalVisualObjects[i]);
            }
            orbitalVisualObjects = null;
        }

        private void DestroyAreaCircleVFX(SkillCastType castType)
        {
            if (!areaCircleVfxByType.TryGetValue(castType, out AreaCircleVFX vfx)) return;
            if (vfx != null) Destroy(vfx.gameObject);
            areaCircleVfxByType.Remove(castType);
        }
    }
}
