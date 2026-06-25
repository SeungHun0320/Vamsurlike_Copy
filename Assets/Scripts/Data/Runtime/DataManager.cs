using System;
using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Stage;

namespace Vamsurlike.Data.Runtime
{
    // 게임 데이터 로더. 런타임 시작 시 Initialize() 한 번 호출.
    // 새 테이블 추가: ① IReadOnlyDictionary 프로퍼티 ② LoadTable 한 줄 ③ Make* 팩토리 메서드
    public static class DataManager
    {
        // ─── Public Tables ───────────────────────────────────────────────

        public static IReadOnlyDictionary<int, EnemyScalingData> EnemyScaling { get; private set; }
        public static IReadOnlyDictionary<int, StageData>        Stages       { get; private set; }
        public static IReadOnlyDictionary<int, WaveData>         Waves        { get; private set; }

        public static bool IsInitialized { get; private set; }

        // ─── Initialize ──────────────────────────────────────────────────

        public static void Initialize()
        {
            if (IsInitialized) return;

            EnemyScaling = LoadTable("Data/EnemyScalingTable", MakeScaling, d => d.Id);
            Stages       = LoadTable("Data/StageTable",        MakeStage,   d => d.Id);
            Waves        = LoadTable("Data/WaveTable",         MakeWave,    d => d.Id);

            DataValidator.Validate(EnemyScaling, Stages, Waves);

            IsInitialized = true;
            Debug.Log($"[DataManager] 초기화 완료 — " +
                      $"Scaling:{EnemyScaling.Count} Stage:{Stages.Count} Wave:{Waves.Count}");
        }

        // ─── Scaling Lookup (시간 기반 스텝 함수) ────────────────────────

        public static EnemyScalingData GetScaling(float elapsedSeconds)
        {
            float            elapsedMinutes = elapsedSeconds / 60f;
            EnemyScalingData result         = null;

            foreach (var row in EnemyScaling.Values)
            {
                if (row.TimeMinutes > elapsedMinutes) break;
                result = row;
            }

            return result ?? new EnemyScalingData(0, 0f, 1f, 1f, 1f);
        }

        // ─── Wave Group Lookup ───────────────────────────────────────────

        public static List<WaveData> GetWaveSequence(int groupId)
        {
            var result = new List<WaveData>();
            foreach (var wave in Waves.Values)
                if (wave.WaveGroupId == groupId) result.Add(wave);

            result.Sort((a, b) => a.SequenceIndex.CompareTo(b.SequenceIndex));
            return result;
        }

        // ─── Generic Loader ──────────────────────────────────────────────

        private static Dictionary<int, T> LoadTable<T>(
            string resourcePath,
            Func<string[], Dictionary<string, int>, int, T> factory,
            Func<T, int> getId)
        {
            string csv  = LoadCSV(resourcePath);
            var    dict = new Dictionary<int, T>();
            Dictionary<string, int> header = null;

            foreach (var (cols, lineNum) in CSVParser.ReadRows(csv))
            {
                if (header == null) { header = CSVParser.ParseHeader(cols); continue; }
                T item  = factory(cols, header, lineNum);
                dict[getId(item)] = item;
            }
            return dict;
        }

        private static string LoadCSV(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
                throw new Exception($"[DataManager] CSV 없음: Resources/{resourcePath}");
            return asset.text;
        }

        // ─── Factory Methods ─────────────────────────────────────────────

        private static EnemyScalingData MakeScaling(string[] cols, Dictionary<string, int> h, int line)
        {
            int   id    = CSVParser.Int  (cols, h["id"],                  "id",                  line);
            float t     = CSVParser.Float(cols, h["timeMinutes"],          "timeMinutes",         line);
            float hp    = CSVParser.Float(cols, h["hpMultiplier"],        "hpMultiplier",        line);
            float dmg   = CSVParser.Float(cols, h["damageMultiplier"],    "damageMultiplier",    line);
            float spawn = CSVParser.Float(cols, h["spawnRateMultiplier"], "spawnRateMultiplier", line);
            return new EnemyScalingData(id, t,
                Mathf.Max(0.1f, hp), Mathf.Max(0.1f, dmg), Mathf.Max(0.1f, spawn));
        }

        private static StageData MakeStage(string[] cols, Dictionary<string, int> h, int line)
        {
            int    id      = CSVParser.Int  (cols, h["id"],              "id",              line);
            string name    = CSVParser.Str  (cols, h["stageName"]);
            float  dur     = CSVParser.Float(cols, h["durationSeconds"], "durationSeconds", line);
            int    groupId = CSVParser.Int  (cols, h["waveGroupId"],     "waveGroupId",     line);
            string boss    = CSVParser.Str  (cols, h["bossEnemyName"]);
            var    cond    = CSVParser.Enum<StageClearCondition>(
                                 cols, h["clearCondition"], "clearCondition", line);
            return new StageData(id, name, Mathf.Max(1f, dur), groupId, boss, cond);
        }

        private static WaveData MakeWave(string[] cols, Dictionary<string, int> h, int line)
        {
            int    id      = CSVParser.Int  (cols, h["id"],            "id",            line);
            int    groupId = CSVParser.Int  (cols, h["waveGroupId"],   "waveGroupId",   line);
            int    seqIdx  = CSVParser.Int  (cols, h["sequenceIndex"], "sequenceIndex", line);
            float  dur     = CSVParser.Float(cols, h["waveDuration"],  "waveDuration",  line);
            bool   loop    = CSVParser.Bool (cols, h["loopFromHere"]);
            string action  = CSVParser.Str  (cols, h["spawnActionName"]);
            string raw     = CSVParser.Str  (cols, h["entries"]);

            var rawList = CSVParser.ParseEntries(raw);
            var entries = new List<WaveEntry>(rawList.Count);
            foreach (var (eName, cnt, ivl) in rawList)
                entries.Add(new WaveEntry(eName, Mathf.Max(1, cnt), Mathf.Max(0f, ivl)));

            return new WaveData(id, groupId, seqIdx, Mathf.Max(0f, dur), loop, action, entries);
        }
    }
}
