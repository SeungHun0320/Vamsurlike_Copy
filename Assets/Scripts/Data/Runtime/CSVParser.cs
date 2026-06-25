using System;
using System.Collections.Generic;
using System.Globalization;

namespace Vamsurlike.Data.Runtime
{
    // CSV 문자열을 행/열로 분해하는 유틸리티.
    // 파싱 로직(타입 변환)은 DataManager 쪽에서 명시적으로 작성해 오류 메시지를 명확하게 유지한다.
    public static class CSVParser
    {
        public static IEnumerable<(string[] columns, int lineNumber)> ReadRows(string csv)
        {
            int lineNum = 0;
            foreach (string raw in csv.Split('\n'))
            {
                lineNum++;
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                yield return (SplitLine(line), lineNum);
            }
        }

        // 헤더 행을 읽어 컬럼명 → 인덱스 딕셔너리 반환
        public static Dictionary<string, int> ParseHeader(string[] columns)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < columns.Length; i++)
                map[columns[i].Trim()] = i;
            return map;
        }

        public static string   Str(string[] cols, int idx)  => idx < cols.Length ? cols[idx].Trim() : "";
        public static bool     Bool(string[] cols, int idx)  => Str(cols, idx).ToUpperInvariant() == "TRUE";

        public static int Int(string[] cols, int idx, string field, int lineNum)
        {
            string s = Str(cols, idx);
            if (int.TryParse(s, out int v)) return v;
            throw new FormatException($"[CSV] {lineNum}번 줄 '{field}': '{s}' → int 변환 실패");
        }

        public static float Float(string[] cols, int idx, string field, int lineNum)
        {
            string s = Str(cols, idx);
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return v;
            throw new FormatException($"[CSV] {lineNum}번 줄 '{field}': '{s}' → float 변환 실패");
        }

        public static T Enum<T>(string[] cols, int idx, string field, int lineNum) where T : struct
        {
            string s = Str(cols, idx);
            if (System.Enum.TryParse(s, out T v)) return v;
            throw new FormatException($"[CSV] {lineNum}번 줄 '{field}': '{s}' → {typeof(T).Name} 변환 실패");
        }

        // "EnemyData_A:5:0.6|EnemyData_B:3:0.8" 형식 파싱
        public static IReadOnlyList<(string name, int count, float interval)> ParseEntries(string raw)
        {
            var result = new List<(string, int, float)>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            foreach (string part in raw.Split('|'))
            {
                string[] seg = part.Trim().Split(':');
                if (seg.Length < 3) continue;
                string name     = seg[0].Trim();
                if (!int.TryParse(seg[1].Trim(), out int count))    continue;
                if (!float.TryParse(seg[2].Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float ivl)) continue;
                result.Add((name, count, ivl));
            }
            return result;
        }

        private static string[] SplitLine(string line)
        {
            var  result = new List<string>();
            bool inQ    = false;
            int  start  = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"')           { inQ = !inQ; continue; }
                if (line[i] == ',' && !inQ)   { result.Add(line.Substring(start, i - start).Trim('"', ' ')); start = i + 1; }
            }
            result.Add(line.Substring(start).Trim('"', ' '));
            return result.ToArray();
        }
    }
}
