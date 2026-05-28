using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    public class OrbitalNetworkSkill : SkillBase
    {
        [SerializeField] private GameObject orbitalVisualPrefab;
        [SerializeField] private float orbitalHeightOffset = 0.9f;

        private readonly List<EnemyNetworkBase> targets = new();
        private readonly HashSet<ulong> hitEnemyIds = new();

        // 서버: 비주얼 ClientRpc 중복 전송 방지
        private bool  serverVisualBroadcast;
        private int   serverVisualCount;
        private float serverVisualRadius;
        private float serverVisualRotationSpeed;

        // 클라이언트: 로컬 비주얼 오브젝트
        private GameObject[] visualObjects;
        private bool visualsActive;
        private float visualRadius;
        private float visualRotationSpeed;

        protected override SkillCastType SupportedCastType => SkillCastType.Orbital;
        public override bool IsPersistentExecution => true;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            // 서버는 비주얼 불필요. 클라이언트는 ActivateOrbitalsClientRpc 수신 후 생성.
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            DestroyVisuals();
        }

        private void Update()
        {
            if (!visualsActive || visualObjects == null) return;
            UpdateVisualPositions();
        }

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            int orbitalCount = Mathf.Max(1, levelData.orbitalCount);
            float orbitalRadius = Mathf.Max(0.1f, levelData.orbitalRadius);
            float hitRadius = Mathf.Max(0.05f, levelData.orbitalHitRadius);
            float rotationSpeed = levelData.orbitalRotationSpeed;

            // count/radius/speed 중 하나라도 바뀌면 재전송
            if (!serverVisualBroadcast
                || serverVisualCount != orbitalCount
                || !Mathf.Approximately(serverVisualRadius, orbitalRadius)
                || !Mathf.Approximately(serverVisualRotationSpeed, rotationSpeed))
            {
                serverVisualBroadcast      = true;
                serverVisualCount          = orbitalCount;
                serverVisualRadius         = orbitalRadius;
                serverVisualRotationSpeed  = rotationSpeed;
                ActivateOrbitalsClientRpc(orbitalCount, orbitalRadius, rotationSpeed);
            }

            int damagedCount = 0;
            hitEnemyIds.Clear();

            for (int i = 0; i < orbitalCount; i++)
            {
                Vector3 orbPos = GetOrbitalPosition(context.CasterTransform.position, orbitalRadius, rotationSpeed, i, orbitalCount);
                int targetCount = AutoTargeting.FindEnemiesInRange(orbPos, hitRadius, targets);

                for (int j = 0; j < targetCount; j++)
                {
                    EnemyNetworkBase target = targets[j];
                    if (target == null || !hitEnemyIds.Add(target.NetworkObjectId)) continue;
                    target.TakeDamage(levelData.damage);
                    damagedCount++;
                }
            }

            if (damagedCount == 0)
            {
                if (ShouldLogNoTarget())
                    Debug.Log($"[{nameof(OrbitalNetworkSkill)}] No orbital targets. skill={skill.name}, count={orbitalCount}, radius={orbitalRadius}, hitRadius={hitRadius}");
                return false;
            }

            Debug.Log($"[{nameof(OrbitalNetworkSkill)}] Orbital tick. skill={skill.name}, level={context.Level}, damage={levelData.damage}, orbCount={orbitalCount}, damaged={damagedCount}");
            return true;
        }

        [ClientRpc]
        private void ActivateOrbitalsClientRpc(int count, float radius, float rotationSpeed)
        {
            if (orbitalVisualPrefab == null)
            {
                Debug.LogWarning($"[{nameof(OrbitalNetworkSkill)}] orbitalVisualPrefab is not assigned. Skipping visuals.");
                return;
            }

            DestroyVisuals();

            visualRadius = radius;
            visualRotationSpeed = rotationSpeed;
            visualObjects = new GameObject[count];

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetOrbitalPosition(transform.position + Vector3.up * orbitalHeightOffset, radius, rotationSpeed, i, count);
                visualObjects[i] = Instantiate(orbitalVisualPrefab, pos, Quaternion.identity);
            }

            visualsActive = true;
        }

        private void UpdateVisualPositions()
        {
            for (int i = 0; i < visualObjects.Length; i++)
            {
                if (visualObjects[i] == null) continue;
                visualObjects[i].transform.position = GetOrbitalPosition(
                    transform.position + Vector3.up * orbitalHeightOffset, visualRadius, visualRotationSpeed, i, visualObjects.Length);
            }
        }

        private void DestroyVisuals()
        {
            if (visualObjects == null) return;
            for (int i = 0; i < visualObjects.Length; i++)
            {
                if (visualObjects[i] != null)
                    Destroy(visualObjects[i]);
            }
            visualObjects = null;
            visualsActive = false;
        }

        private static Vector3 GetOrbitalPosition(Vector3 origin, float radius, float rotationSpeed, int index, int count)
        {
            float angleStep = 360f / count;
            float angle = Time.time * rotationSpeed + angleStep * index;
            Vector3 offset = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * radius;
            return origin + offset;
        }
    }
}
