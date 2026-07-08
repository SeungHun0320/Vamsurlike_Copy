using System.Collections;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Enemy;

namespace Vamsurlike.Skills
{
    // 합체 스킬: Melee + AttackPower.
    // 망치와 동일한 전방 사각형 판정으로 메인 타격 + 기절을 즉시 적용한 뒤, aftershockDelay만큼
    // 지연시켰다가 스윙 범위 안 무작위 지점에 여진(aftershock) 원형 폭발을 추가로 터뜨린다
    // ("땅을 내려찍고 뒤늦게 균열이 터지는" 컨셉).
    public sealed class EarthshatterSkill : SkillBase
    {
        // RULES.md: 시드 기반 System.Random 사용
        private readonly System.Random rng = new();

        public override SkillCastType SupportedCastType => SkillCastType.Earthshatter;

        public override bool TryExecute(in SkillCastContext context)
        {
            SkillDataSO skill = context.Skill;
            SkillLevelData levelData = context.LevelData;

            if (skill == null || levelData == null || context.CasterTransform == null)
                return false;

            Vector3 origin  = context.CasterTransform.position;
            float   range   = AutoTargeting.ResolveTargetingRange(context);
            Vector3 forward = AutoTargeting.ResolveDirection(context, origin, context.CasterForward, out _);
            float   width   = levelData.meleeWidth * context.AreaMultiplier;
            float   damage  = context.FinalDamage;

            MeleeBoxHit.Apply(origin, forward, range, width, damage, context.OwnerClientId, skill.name,
                enemy => enemy.ApplyStun(levelData.stunDuration));

            context.VFX?.ShowMeleeBox(SupportedCastType, origin, forward, range, width);

            if (levelData.aftershockCount > 0 && context.CoroutineRunner != null)
            {
                context.CoroutineRunner.StartSkillCoroutine(DelayedAftershockCoroutine(
                    origin, forward, range, width, damage, levelData, context.OwnerClientId, skill.name, context.VFX));
            }

            return true;
        }

        private IEnumerator DelayedAftershockCoroutine(
            Vector3 origin, Vector3 forward, float range, float width, float mainDamage,
            SkillLevelData levelData, ulong ownerClientId, string skillTag, ISkillVFXBroadcaster vfx)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, levelData.aftershockDelay));
            SpawnAftershocks(origin, forward, range, width, mainDamage, levelData, ownerClientId, skillTag, vfx);
        }

        private void SpawnAftershocks(
            Vector3 origin, Vector3 forward, float range, float width, float mainDamage,
            SkillLevelData levelData, ulong ownerClientId, string skillTag, ISkillVFXBroadcaster vfx)
        {
            if (levelData.aftershockCount <= 0) return;

            Vector3 flatForward = MeleeBoxHit.FlattenForward(forward);
            Vector3 right = MeleeBoxHit.GetRight(flatForward);
            float halfWidth = width * 0.5f;
            float aftershockDamage = mainDamage * levelData.aftershockDamageRatio;

            for (int i = 0; i < levelData.aftershockCount; i++)
            {
                float localX = (float)(rng.NextDouble() * width - halfWidth);
                float localZ = (float)(rng.NextDouble() * range);
                Vector3 point = origin + right * localX + flatForward * localZ;

                SkillAreaDamage.ApplySplash(point, levelData.aftershockRadius, aftershockDamage, ownerClientId, skillTag);
                vfx?.ShowGrenadeImpactCircle(point, levelData.aftershockRadius, 0.3f);
            }
        }
    }
}
