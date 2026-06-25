using System;
using System.IO;
using System.Net.Http;
using UnityEditor;
using UnityEngine;

namespace Vamsurlike.Editor
{
    // Menu: Vamsurlike > Data > Open Import Window
    // 역할: Google Sheets CSV URL → Assets/Resources/Data/*.csv 로 저장
    //       로컬 편집은 VSCode Edit CSV로 직접 수정 → 런타임에서 바로 로드됨 (DataManager)
    //
    // 새 테이블 추가: Tables 배열에 TableDef 한 줄만 추가하면 됨.
    public class DataImportWindow : EditorWindow
    {
        // ─── Table Definitions ──────────────────────────────────────────────

        public static readonly TableDef[] Tables =
        {
            new() { Key = "WaveTable",         Label = "Wave Table",          CsvPath = "Assets/Resources/Data/WaveTable.csv" },
            new() { Key = "EnemyScalingTable", Label = "Enemy Scaling Table", CsvPath = "Assets/Resources/Data/EnemyScalingTable.csv" },
            new() { Key = "StageTable",        Label = "Stage Table",         CsvPath = "Assets/Resources/Data/StageTable.csv" },
        };

        public struct TableDef
        {
            public string Key;
            public string Label;
            public string CsvPath;
        }

        // ─── EditorWindow ────────────────────────────────────────────────────

        private string[] urls;
        private string   statusMessage = "";
        private bool     statusIsError;

        [MenuItem("Vamsurlike/Data/Open Import Window")]
        public static void Open() => GetWindow<DataImportWindow>("Data Importer");

        private void OnEnable()
        {
            urls = new string[Tables.Length];
            for (int i = 0; i < Tables.Length; i++)
                urls[i] = EditorPrefs.GetString(PrefKey(Tables[i].Key), "");
        }

        private void OnGUI()
        {
            GUILayout.Label("Data Table Importer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Google Sheets → 파일 → 웹에 게시 → CSV URL 붙여넣기 후 Import\n" +
                "로컬 편집: VSCode Edit CSV 확장으로 CsvPath 파일 직접 수정",
                MessageType.Info);

            GUILayout.Space(6);

            if (GUILayout.Button("Import All from Google Sheets", GUILayout.Height(28)))
                RunAll();

            GUILayout.Space(10);

            for (int i = 0; i < Tables.Length; i++)
                DrawTableRow(i);

            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(8);
                EditorGUILayout.HelpBox(statusMessage,
                    statusIsError ? MessageType.Error : MessageType.Info);
            }
        }

        private void DrawTableRow(int i)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(Tables[i].Label, EditorStyles.boldLabel);
            GUILayout.Label($"  {Tables[i].CsvPath}", EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            urls[i] = EditorGUILayout.TextField("Sheet URL", urls[i]);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString(PrefKey(Tables[i].Key), urls[i]);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import from URL"))
                RunTable(i);
            if (GUILayout.Button("Open CSV", GUILayout.Width(80)))
                OpenLocalCSV(Tables[i].CsvPath);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(4);
        }

        private void RunAll()
        {
            int ok = 0;
            for (int i = 0; i < Tables.Length; i++)
                if (RunTable(i)) ok++;
            SetStatus($"Import All 완료: {ok}/{Tables.Length} 성공", ok < Tables.Length);
        }

        private bool RunTable(int i)
        {
            string url = urls[i];
            if (string.IsNullOrWhiteSpace(url))
            {
                SetStatus($"{Tables[i].Label}: URL이 비어 있습니다.", true);
                return false;
            }

            string csv = FetchUrl(url, Tables[i].Label);
            if (csv == null) return false;

            try
            {
                WriteCSV(Tables[i].CsvPath, csv);
                SetStatus($"{Tables[i].Label} 다운로드 완료 → {Tables[i].CsvPath}", false);
                return true;
            }
            catch (Exception e)
            {
                SetStatus($"{Tables[i].Label} 저장 오류: {e.Message}", true);
                Debug.LogException(e);
                return false;
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private string FetchUrl(string url, string label)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                return client.GetStringAsync(url).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                SetStatus($"{label} 다운로드 실패: {e.Message}", true);
                Debug.LogError($"[DataImportWindow] {label} 다운로드 실패: {e.Message}");
                return null;
            }
        }

        private static void WriteCSV(string assetPath, string content)
        {
            string fullPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../", assetPath));
            File.WriteAllText(fullPath, content, System.Text.Encoding.UTF8);
            AssetDatabase.ImportAsset(assetPath);
            Debug.Log($"[DataImportWindow] 저장 완료: {assetPath}");
        }

        private static void OpenLocalCSV(string csvPath)
        {
            string full = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../", csvPath));
            if (File.Exists(full))
                System.Diagnostics.Process.Start(full);
            else
                Debug.LogWarning($"[DataImportWindow] 로컬 CSV 없음: {csvPath}");
        }

        private void SetStatus(string msg, bool isError)
        {
            statusMessage = msg;
            statusIsError = isError;
            Repaint();
        }

        private static string PrefKey(string tableKey) => $"DataImport_{tableKey}_url";
    }
}
