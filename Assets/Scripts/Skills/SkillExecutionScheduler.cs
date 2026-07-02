using System;
using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Skills
{
    internal sealed class SkillExecutionScheduler
    {
        private float nextWarningLogTime;

        public void Tick(
            IReadOnlyList<SkillRuntimeState> skills,
            float failedCastRetryDelay,
            Func<SkillDataSO, bool> isPersistent,
            Func<SkillRuntimeState, SkillLevelData, bool> tryCast,
            Action<SkillRuntimeState> onPersistentDeactivated,
            Action<string> logWarning,
            float durationMultiplier = 1f,
            float cooldownMultiplier = 1f)
        {
            if (skills == null) return;

            for (int i = 0; i < skills.Count; i++)
            {
                SkillRuntimeState owned = skills[i];
                if (owned.Skill == null)
                {
                    LogThrottled(logWarning, $"skill[{i}] is null.");
                    continue;
                }

                SkillLevelData levelData = owned.Skill.GetLevelData(owned.Level);
                if (isPersistent != null && isPersistent(owned.Skill))
                    TickPersistent(owned, levelData, tryCast, onPersistentDeactivated, durationMultiplier, cooldownMultiplier);
                else
                    TickCooldown(owned, levelData, failedCastRetryDelay, tryCast, cooldownMultiplier);
            }
        }

        private static void TickPersistent(
            SkillRuntimeState owned,
            SkillLevelData levelData,
            Func<SkillRuntimeState, SkillLevelData, bool> tryCast,
            Action<SkillRuntimeState> onPersistentDeactivated,
            float durationMultiplier,
            float cooldownMultiplier)
        {
            if (levelData == null) return;

            if (owned.IsActive)
            {
                if (owned.DurationTimer < 0f)
                    owned.DurationTimer = levelData.duration * durationMultiplier;

                owned.TickTimer -= Time.deltaTime;
                if (owned.TickTimer <= 0f)
                {
                    tryCast?.Invoke(owned, levelData);
                    owned.TickTimer = levelData.tickInterval;
                }

                if (levelData.duration <= 0f) return;

                owned.DurationTimer -= Time.deltaTime;
                if (owned.DurationTimer > 0f) return;

                owned.IsActive = false;
                owned.CooldownTimer = levelData.cooldown * cooldownMultiplier;
                // 지속시간 종료 — 다음 활성화까지 비주얼을 꺼둔다 (재활성화 시 실행자가 다시 표시).
                onPersistentDeactivated?.Invoke(owned);
                return;
            }

            owned.CooldownTimer -= Time.deltaTime;
            if (owned.CooldownTimer > 0f) return;

            owned.IsActive = true;
            owned.DurationTimer = levelData.duration * durationMultiplier;
            owned.TickTimer = 0f;
        }

        private static void TickCooldown(
            SkillRuntimeState owned,
            SkillLevelData levelData,
            float failedCastRetryDelay,
            Func<SkillRuntimeState, SkillLevelData, bool> tryCast,
            float cooldownMultiplier)
        {
            owned.CooldownTimer -= Time.deltaTime;
            if (owned.Skill.isManual || owned.CooldownTimer > 0f) return;

            bool casted = tryCast != null && tryCast(owned, levelData);
            owned.CooldownTimer = casted
                ? (levelData != null ? levelData.cooldown * cooldownMultiplier : 1f)
                : failedCastRetryDelay;
        }

        private void LogThrottled(Action<string> logWarning, string message)
        {
            if (Time.time < nextWarningLogTime) return;

            nextWarningLogTime = Time.time + 2f;
            logWarning?.Invoke(message);
        }
    }
}
