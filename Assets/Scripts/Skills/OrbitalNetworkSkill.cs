using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    public sealed class OrbitalSkill : SkillBase
    {
        private readonly GameObject visualPrefab;
        private readonly float heightOffset;

        private readonly List<EnemyNetworkBase> targets = new();
        private readonly HashSet<ulong> hitEnemyIds = new();

        // 서버: ClientRpc 중복 전송 방지용 캐시
        private bool serverBroadcastSent;
        private int serverCount;
        private float serverRadius;
        private float serverRotSpeed;

        // 클라이언트: 로컬 비주얼 상태
        private GameObject[] visualObjects;
        private bool visualsActive;
        private float visualRadius;
        private float visualRotSpeed;

        public OrbitalSkill(GameObject visualPrefab, float heightOffset)
        {
            this.visualPrefab = visualPrefab;
            this.heightOffset = heightOffset;
        }

        public override SkillCastType SupportedCastType => SkillCastType.Orbital;
        public override bool IsPersistentExecution => true;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            int count = Mathf.Max(1, levelData.orbitalCount);
            float radius = Mathf.Max(0.1f, levelData.orbitalRadius);
            float hitRadius = Mathf.Max(0.05f, levelData.orbitalHitRadius);
            float rotSpeed = levelData.orbitalRotationSpeed;

            // count/radius/rotSpeed 중 하나라도 바뀌면 클라이언트에 재전송
            if (!serverBroadcastSent
                || serverCount != count
                || !Mathf.Approximately(serverRadius, radius)
                || !Mathf.Approximately(serverRotSpeed, rotSpeed))
            {
                serverBroadcastSent = true;
                serverCount = count;
                serverRadius = radius;
                serverRotSpeed = rotSpeed;
                context.Manager.BroadcastOrbitalClientRpc(count, radius, rotSpeed);
            }

            int damagedCount = 0;
            hitEnemyIds.Clear();

            for (int i = 0; i < count; i++)
            {
                Vector3 orbPos = GetOrbitalPosition(context.CasterTransform.position, radius, rotSpeed, i, count);
                int targetCount = AutoTargeting.FindEnemiesInRange(orbPos, hitRadius, targets);

                float damage = context.FinalDamage;
                for (int j = 0; j < targetCount; j++)
                {
                    EnemyNetworkBase target = targets[j];
                    if (target == null || !hitEnemyIds.Add(target.NetworkObjectId)) continue;
                    target.TakeDamage(damage);
                    damagedCount++;
                }
            }

            if (damagedCount == 0)
            {
                if (ShouldLogNoTarget())
                    Debug.Log($"[{nameof(OrbitalSkill)}] 궤도체 범위 내 적 없음. skill={skill.name}, count={count}, radius={radius}");
                return false;
            }

            Debug.Log($"[{nameof(OrbitalSkill)}] 틱. skill={skill.name}, level={context.Level}, damage={context.FinalDamage}(x{context.AttackMultiplier}), orbCount={count}, damaged={damagedCount}");
            return true;
        }

        // SkillManager.BroadcastOrbitalClientRpc가 모든 클라이언트에서 호출
        public void OnClientOrbitalActivated(int count, float radius, float rotSpeed, Transform ownerTransform)
        {
            if (visualPrefab == null)
            {
                Debug.LogWarning($"[{nameof(OrbitalSkill)}] orbitalVisualPrefab 미할당.");
                return;
            }

            DestroyVisuals();
            visualRadius = radius;
            visualRotSpeed = rotSpeed;
            visualObjects = new GameObject[count];

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetOrbitalPosition(
                    ownerTransform.position + Vector3.up * heightOffset, radius, rotSpeed, i, count);
                visualObjects[i] = Object.Instantiate(visualPrefab, pos, Quaternion.identity);
            }

            visualsActive = true;
        }

        // SkillManager.Update()가 매 프레임 호출 — 클라이언트 비주얼 위치 갱신
        public override void OnUpdate(Transform ownerTransform)
        {
            if (!visualsActive || visualObjects == null) return;

            for (int i = 0; i < visualObjects.Length; i++)
            {
                if (visualObjects[i] == null) continue;
                visualObjects[i].transform.position = GetOrbitalPosition(
                    ownerTransform.position + Vector3.up * heightOffset,
                    visualRadius, visualRotSpeed, i, visualObjects.Length);
            }
        }

        // SkillManager.OnNetworkDespawn()이 호출
        public override void OnDespawn() => DestroyVisuals();

        private void DestroyVisuals()
        {
            if (visualObjects == null) return;
            for (int i = 0; i < visualObjects.Length; i++)
            {
                if (visualObjects[i] != null)
                    Object.Destroy(visualObjects[i]);
            }
            visualObjects = null;
            visualsActive = false;
        }

        private static Vector3 GetOrbitalPosition(Vector3 origin, float radius, float rotSpeed, int index, int count)
        {
            float angle = Time.time * rotSpeed + 360f / count * index;
            Vector3 offset = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * radius;
            return origin + offset;
        }
    }
}
