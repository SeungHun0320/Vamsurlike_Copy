using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vamsurlike.Data;
using Vamsurlike.Stage;
using Vamsurlike.UI;

namespace Vamsurlike.Editor
{
    // Menu: Vamsurlike > Phase 7 > Setup Stage Assets
    // 1회 실행으로 Phase 7에 필요한 모든 에셋과 씬 오브젝트를 자동 생성/할당.
    public static class SetupPhase7Assets
    {
        private const string DataDir    = "Assets/Data/Stages";
        private const string EnemyDir   = "Assets/Data/Enemies";

        // 기존 EnemyDataSO GUID (EnemyData_A/B/C.asset.meta에서 확인)
        private const string GuidEnemyA = "0c0ecaacbbf42954db3a12cd3b7fab16"; // Enemy
        private const string GuidEnemyB = "f9e28cacf7b8ed04eb3f091537a8bcd4"; // Scout
        private const string GuidEnemyC = "1e87419dddc6b854a9de8b91d5ead860"; // Brute

        private const string StagePath = "Assets/Scenes/Stage_01.unity";

        [MenuItem("Vamsurlike/Phase 7/Setup Stage Assets")]
        public static void Run()
        {
            // Stage_01 씬이 로드되어 있지 않으면 열기
            bool sceneWasOpened = false;
            if (!IsSceneLoaded(StagePath))
            {
                if (!System.IO.File.Exists(StagePath))
                {
                    Debug.LogError($"[SetupPhase7Assets] {StagePath} 를 찾을 수 없습니다.");
                    return;
                }
                EditorSceneManager.OpenScene(StagePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                sceneWasOpened = true;
                Debug.Log($"[SetupPhase7Assets] {StagePath} 씬을 자동으로 열었습니다.");
            }

            // 1. 적 에셋 로드
            var enemyA = LoadByGuid<EnemyDataSO>(GuidEnemyA);
            var enemyB = LoadByGuid<EnemyDataSO>(GuidEnemyB);
            var enemyC = LoadByGuid<EnemyDataSO>(GuidEnemyC);

            if (enemyA == null || enemyB == null || enemyC == null)
            {
                Debug.LogError("[SetupPhase7Assets] EnemyDataSO 로드 실패 — GUID를 확인하세요.");
                return;
            }

            EnsureDirectory(DataDir);

            // 2. 보스 EnemyDataSO (Brute 복사 후 스탯 강화)
            var bossData = CreateBossData(enemyC);

            // 3. 테이블 에셋 생성
            var stageTable   = CreateStageTable(bossData);
            var waveTable    = CreateWaveTable(enemyA, enemyB, enemyC);
            var scalingTable = CreateScalingTable();

            AssetDatabase.SaveAssets();

            // 4. 씬 컴포넌트에 자동 할당
            AssignToScene(stageTable, waveTable, scalingTable);

            // 5. StageResultUI 씬 오브젝트 생성
            CreateStageResultUI();

            AssetDatabase.Refresh();

            // 씬 자동 저장 (사용자 확인 후)
            bool saved = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            Debug.Log($"[SetupPhase7Assets] ✅ 완료 — 씬 저장={saved}");
            EditorUtility.DisplayDialog(
                "Phase 7 Setup 완료",
                "모든 에셋이 생성되고 씬에 자동 할당되었습니다.\n\n• StageTable.asset → bossData 확인\n• WaveTable.asset → entries 확인\n• StageResultPanel UI 텍스트 스타일 조정 (선택)",
                "확인");
        }

        // ─── Boss EnemyDataSO ────────────────────────────────────────────
        private static EnemyDataSO CreateBossData(EnemyDataSO bruteSource)
        {
            string path = $"{EnemyDir}/BossData_01.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
            if (existing != null)
            {
                Debug.Log("[SetupPhase7Assets] BossData_01.asset 이미 존재 — 재사용");
                return existing;
            }

            // Brute 복사 → 보스 전용 수치로 덮어쓰기
            string sourcePath = AssetDatabase.GUIDToAssetPath(GuidEnemyC);
            AssetDatabase.CopyAsset(sourcePath, path);

            var boss   = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
            var bossSO = new SerializedObject(boss);
            bossSO.FindProperty("enemyName").stringValue      = "Boss";
            bossSO.FindProperty("hp").floatValue              = 1000f;
            bossSO.FindProperty("moveSpeed").floatValue       = 3f;
            bossSO.FindProperty("attackPower").floatValue     = 40f;
            bossSO.FindProperty("defense").floatValue         = 10f;
            bossSO.FindProperty("attackRange").floatValue     = 2f;
            bossSO.FindProperty("attackInterval").floatValue  = 1.2f;
            bossSO.FindProperty("xpDrop").intValue            = 500;
            bossSO.FindProperty("isBoss").boolValue           = true;
            bossSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(boss);

            Debug.Log($"[SetupPhase7Assets] {path} 생성 (HP=1000, isBoss=true)");
            return boss;
        }

        // ─── StageTable ──────────────────────────────────────────────────
        private static StageTableSO CreateStageTable(EnemyDataSO bossData)
        {
            string path = $"{DataDir}/StageTable.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StageTableSO>(path);
            if (existing != null)
            {
                Debug.Log("[SetupPhase7Assets] StageTable.asset 이미 존재 — bossData만 갱신");
                var exSO = new SerializedObject(existing);
                var exRows = exSO.FindProperty("rows");
                if (exRows.arraySize > 0)
                    exRows.GetArrayElementAtIndex(0).FindPropertyRelative("bossData").objectReferenceValue = bossData;
                exSO.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var so         = ScriptableObject.CreateInstance<StageTableSO>();
            var serialized = new SerializedObject(so);
            var rows       = serialized.FindProperty("rows");
            rows.arraySize = 1;
            var row0       = rows.GetArrayElementAtIndex(0);
            row0.FindPropertyRelative("stageId").intValue           = 1;
            row0.FindPropertyRelative("stageName").stringValue      = "Stage 01 — Survival";
            row0.FindPropertyRelative("durationSeconds").floatValue = 300f;   // 5분
            row0.FindPropertyRelative("waveGroupId").intValue       = 0;
            row0.FindPropertyRelative("bossData").objectReferenceValue = bossData;
            row0.FindPropertyRelative("clearCondition").enumValueIndex = (int)StageClearCondition.BossKill;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[SetupPhase7Assets] {path} 생성");
            return so;
        }

        // ─── WaveTable ───────────────────────────────────────────────────
        private static WaveTableSO CreateWaveTable(EnemyDataSO a, EnemyDataSO b, EnemyDataSO c)
        {
            string path = $"{DataDir}/WaveTable.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WaveTableSO>(path);
            if (existing != null)
            {
                Debug.Log("[SetupPhase7Assets] WaveTable.asset 이미 존재 — 덮어쓰지 않음");
                return existing;
            }

            var so         = ScriptableObject.CreateInstance<WaveTableSO>();
            var serialized = new SerializedObject(so);
            var rows       = serialized.FindProperty("rows");

            // 4개 웨이브 정의
            // Wave 0 — 초반: Enemy(A)×5
            // Wave 1 — 중반: Enemy(A)×5 + Scout(B)×4
            // Wave 2 — 루프 시작: Scout(B)×6 + Brute(C)×3
            // Wave 3 — 엘리트 링: Brute(C)×8 원형 포위
            var waveDefs = new WaveDef[]
            {
                new WaveDef
                {
                    seq=0, dur=20f, loop=false, action="",
                    entries=new[]
                    {
                        new EntryDef { data=a, count=5, interval=0.6f },
                    }
                },
                new WaveDef
                {
                    seq=1, dur=25f, loop=false, action="",
                    entries=new[]
                    {
                        new EntryDef { data=a, count=5, interval=0.5f },
                        new EntryDef { data=b, count=4, interval=0.4f },
                    }
                },
                new WaveDef
                {
                    seq=2, dur=30f, loop=true, action="",
                    entries=new[]
                    {
                        new EntryDef { data=b, count=6, interval=0.4f },
                        new EntryDef { data=c, count=3, interval=0.8f },
                    }
                },
                new WaveDef
                {
                    seq=3, dur=35f, loop=false, action="SpawnEliteRing",
                    entries=new[]
                    {
                        new EntryDef { data=c, count=8, interval=0f },
                    }
                },
            };

            rows.arraySize = waveDefs.Length;
            for (int i = 0; i < waveDefs.Length; i++)
            {
                var d   = waveDefs[i];
                var row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("waveGroupId").intValue        = 0;
                row.FindPropertyRelative("sequenceIndex").intValue      = d.seq;
                row.FindPropertyRelative("waveDuration").floatValue     = d.dur;
                row.FindPropertyRelative("loopFromHere").boolValue      = d.loop;
                row.FindPropertyRelative("spawnActionName").stringValue = d.action;

                var entriesProp   = row.FindPropertyRelative("entries");
                entriesProp.arraySize = d.entries.Length;
                for (int j = 0; j < d.entries.Length; j++)
                {
                    var e     = d.entries[j];
                    var entry = entriesProp.GetArrayElementAtIndex(j);
                    entry.FindPropertyRelative("enemyData").objectReferenceValue = e.data;
                    entry.FindPropertyRelative("count").intValue                 = e.count;
                    entry.FindPropertyRelative("spawnInterval").floatValue       = e.interval;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[SetupPhase7Assets] {path} 생성 (웨이브 4개)");
            return so;
        }

        // ─── EnemyScalingTable ───────────────────────────────────────────
        private static EnemyScalingTableSO CreateScalingTable()
        {
            string path = $"{DataDir}/EnemyScalingTable.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyScalingTableSO>(path);
            if (existing != null) { Debug.Log("[SetupPhase7Assets] EnemyScalingTable.asset 이미 존재"); return existing; }

            var so         = ScriptableObject.CreateInstance<EnemyScalingTableSO>();
            var serialized = new SerializedObject(so);
            var rows       = serialized.FindProperty("rows");

            var defs = new[] {
                (0f,  1.00f, 1.00f, 1.00f),
                (2f,  1.30f, 1.20f, 1.40f),
                (5f,  1.75f, 1.50f, 2.00f),
                (10f, 2.50f, 2.00f, 3.00f),
                (15f, 3.50f, 2.50f, 4.00f),
                (20f, 5.00f, 3.00f, 5.00f),
            };

            rows.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++)
            {
                var (min, hp, dmg, rate) = defs[i];
                var row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("timeMinutes").floatValue         = min;
                row.FindPropertyRelative("hpMultiplier").floatValue        = hp;
                row.FindPropertyRelative("damageMultiplier").floatValue    = dmg;
                row.FindPropertyRelative("spawnRateMultiplier").floatValue = rate;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[SetupPhase7Assets] {path} 생성 (스케일링 {defs.Length}행)");
            return so;
        }

        // ─── Scene Assignment ────────────────────────────────────────────
        private static void AssignToScene(StageTableSO stageTable, WaveTableSO waveTable, EnemyScalingTableSO scalingTable)
        {
            int runtimeCount = 0;
            foreach (var runtime in Object.FindObjectsByType<StageRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(runtime, "Assign StageTable");
                var so   = new SerializedObject(runtime);
                var prop = so.FindProperty("stageTable");
                if (prop != null)
                {
                    prop.objectReferenceValue = stageTable;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(runtime);
                    runtimeCount++;
                    Debug.Log($"[SetupPhase7Assets] StageRuntime({runtime.name}) → stageTable 할당");
                }
            }
            if (runtimeCount == 0)
                Debug.LogWarning("[SetupPhase7Assets] ⚠️ StageRuntime을 씬에서 찾지 못했습니다. Stage_01 씬을 열고 다시 실행하세요.");

            int ctrlCount = 0;
            foreach (var ctrl in Object.FindObjectsByType<WaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(ctrl, "Assign WaveTables");
                var so = new SerializedObject(ctrl);
                var wt = so.FindProperty("waveTable");
                var st = so.FindProperty("scalingTable");
                if (wt != null) wt.objectReferenceValue = waveTable;
                if (st != null) st.objectReferenceValue = scalingTable;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(ctrl);
                ctrlCount++;
                Debug.Log($"[SetupPhase7Assets] WaveController({ctrl.name}) → waveTable + scalingTable 할당");
            }
            if (ctrlCount == 0)
                Debug.LogWarning("[SetupPhase7Assets] ⚠️ WaveController을 씬에서 찾지 못했습니다. Stage_01 씬을 열고 다시 실행하세요.");
        }

        // ─── StageResultUI 씬 오브젝트 ───────────────────────────────────
        // 구조: StageResultRoot(항상 활성, StageResultUI 보유)
        //          └─ StageResultPanel(평소 비활성, Clear/GameOver 시 활성화)
        //                 └─ ResultText (TMP)
        private static void CreateStageResultUI()
        {
            // 이미 있으면 건너뜀
            var existing = Object.FindObjectsByType<StageResultUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing.Length > 0)
            {
                Debug.Log("[SetupPhase7Assets] StageResultUI 이미 존재 — 건너뜀");
                return;
            }

            // Canvas 탐색 (Stage 씬에 이미 Canvas가 있음)
            var allCanvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allCanvas.Length == 0)
            {
                Debug.LogWarning("[SetupPhase7Assets] Canvas를 찾을 수 없음 — StageResultUI 수동 배치 필요");
                return;
            }
            // 렌더 모드가 Screen Space인 Canvas 우선 선택
            Canvas canvas = allCanvas[0];
            foreach (var c in allCanvas)
                if (c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }

            // ── Root (항상 활성 — Start()에서 이벤트 구독 가능) ──
            var rootGO = new GameObject("StageResultRoot");
            rootGO.transform.SetParent(canvas.transform, false);
            var rootRect = rootGO.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // ── Panel (평소 비활성) ──
            var panelGO  = new GameObject("StageResultPanel");
            panelGO.transform.SetParent(rootGO.transform, false);
            var panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImg  = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.7f);
            panelGO.SetActive(false);

            // ── Text ──
            var textGO   = new GameObject("ResultText");
            textGO.transform.SetParent(panelGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin        = new Vector2(0.5f, 0.5f);
            textRect.anchorMax        = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta        = new Vector2(600f, 150f);
            var tmp       = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = "RESULT";
            tmp.fontSize  = 72f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;

            // ── StageResultUI 컴포넌트는 항상 활성인 Root에 ──
            var uiComp = rootGO.AddComponent<StageResultUI>();
            var uiSO   = new SerializedObject(uiComp);
            uiSO.FindProperty("resultPanel").objectReferenceValue = panelGO;
            uiSO.FindProperty("resultText").objectReferenceValue  = tmp;
            uiSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(uiComp);

            Debug.Log("[SetupPhase7Assets] StageResultRoot/Panel 생성 완료");
        }

        // ─── Utilities ───────────────────────────────────────────────────
        private static T LoadByGuid<T>(string guid) where T : Object
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static bool IsSceneLoaded(string scenePath)
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.path == scenePath && scene.isLoaded) return true;
            }
            return false;
        }

        private struct WaveDef
        {
            public int seq; public float dur; public bool loop; public string action;
            public EntryDef[] entries;
        }

        private struct EntryDef
        {
            public EnemyDataSO data; public int count; public float interval;
        }
    }
}
