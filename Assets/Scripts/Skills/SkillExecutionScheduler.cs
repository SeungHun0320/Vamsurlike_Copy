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
            Action<string> logWarning)
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
                    TickPersistent(owned, levelData, tryCast);
                else
                    TickCooldown(owned, levelData, failedCastRetryDelay, tryCast);
            }
        }

        private static void TickPersistent(
            SkillRuntimeState owned,
            SkillLevelData levelData,
            Func<SkillRuntimeState, SkillLevelData, bool> tryCast)
        {
            if (levelData == null) return;

            if (owned.IsActive)
            {
                if (owned.DurationTimer < 0f)
                    owned.DurationTimer = levelData.duration;

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
                owned.CooldownTimer = levelData.cooldown;
                return;
            }

            owned.CooldownTimer -= Time.deltaTime;
            if (owned.CooldownTimer > 0f) return;

            owned.IsActive = true;
            owned.DurationTimer = levelData.duration;
            owned.TickTimer = 0f;
        }

        private static void TickCooldown(
            SkillRuntimeState owned,
            SkillLevelData levelData,
            float failedCastRetryDelay,
            Func<SkillRuntimeState, SkillLevelData, bool> tryCast)
        {
            owned.CooldownTimer -= Time.deltaTime;
            if (owned.Skill.isManual || owned.CooldownTimer > 0f) return;

            bool casted = tryCast != null && tryCast(owned, levelData);
            owned.CooldownTimer = casted
                ? levelData != null ? levelData.cooldown : 1f
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
