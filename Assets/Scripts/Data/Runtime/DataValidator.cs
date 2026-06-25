using System.Collections.Generic;
using UnityEngine;

namespace Vamsurlike.Data.Runtime
{
    // 데이터 적재 직후 정합성 검증. 오류는 LogError, 경고는 LogWarning으로 출력.
    public static class DataValidator
    {
        public static void Validate(
            IReadOnlyDictionary<int, EnemyScalingData> scaling,
            IReadOnlyDictionary<int, StageData>        stages,
            IReadOnlyDictionary<int, WaveData>         waves)
        {
            int errors = 0;

            errors += ValidateScaling(scaling);
            errors += ValidateStages(stages, waves);
            errors += ValidateWaves(waves);

            if (errors > 0)
                Debug.LogError($"[DataValidator] 검증 실패 — {errors}개 오류. 콘솔을 확인하세요.");
            else
                Debug.Log("[DataValidator] 검증 통과");
        }

        private static int ValidateScaling(IReadOnlyDictionary<int, EnemyScalingData> scaling)
        {
            int errors = 0;
            if (scaling.Count == 0)
            {
                Debug.LogError("[DataValidator] EnemyScalingTable이 비어 있습니다.");
                return 1;
            }

            float prevTime = -1f;
            foreach (var row in scaling.Values)
            {
                // timeMinutes 오름차순 검증
                if (row.TimeMinutes < prevTime)
                {
                    Debug.LogError(
                        $"[DataValidator] EnemyScaling id={row.Id}: " +
                        $"timeMinutes({row.TimeMinutes})이 이전 행보다 작습니다. 오름차순으로 정렬하세요.");
                    errors++;
                }
                prevTime = row.TimeMinutes;
            }
            return errors;
        }

        private static int ValidateStages(
            IReadOnlyDictionary<int, StageData> stages,
            IReadOnlyDictionary<int, WaveData>  waves)
        {
            int errors = 0;
            foreach (var stage in stages.Values)
            {
                if (string.IsNullOrEmpty(stage.StageName))
                {
                    Debug.LogError($"[DataValidator] Stage id={stage.Id}: stageName이 비어 있습니다.");
                    errors++;
                }

                // 참조하는 waveGroupId가 실제로 존재하는지
                bool groupExists = false;
                foreach (var wave in waves.Values)
                {
                    if (wave.WaveGroupId == stage.WaveGroupId) { groupExists = true; break; }
                }
                if (!groupExists)
                {
                    Debug.LogError(
                        $"[DataValidator] Stage id={stage.Id}: " +
                        $"waveGroupId={stage.WaveGroupId}에 해당하는 웨이브가 없습니다.");
                    errors++;
                }
            }
            return errors;
        }

        private static int ValidateWaves(IReadOnlyDictionary<int, WaveData> waves)
        {
            int errors = 0;
            foreach (var wave in waves.Values)
            {
                if (wave.Entries.Count == 0 && string.IsNullOrEmpty(wave.SpawnActionName))
                {
                    Debug.LogWarning(
                        $"[DataValidator] Wave id={wave.Id} " +
                        $"(group={wave.WaveGroupId}, seq={wave.SequenceIndex}): " +
                        $"entries도 spawnActionName도 없습니다.");
                }

                foreach (var entry in wave.Entries)
                {
                    if (string.IsNullOrEmpty(entry.EnemyName))
                    {
                        Debug.LogError(
                            $"[DataValidator] Wave id={wave.Id}: entries에 빈 enemyName이 있습니다.");
                        errors++;
                    }
                    if (entry.Count <= 0)
                    {
                        Debug.LogError(
                            $"[DataValidator] Wave id={wave.Id}, enemy={entry.EnemyName}: count={entry.Count}는 1 이상이어야 합니다.");
                        errors++;
                    }
                }
            }
            return errors;
        }
    }
}
