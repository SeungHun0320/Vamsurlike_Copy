using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vamsurlike.Data;
using Vamsurlike.UI;

namespace Vamsurlike.Editor
{
    // Menu: Vamsurlike > Phase 7 > Setup Stage Assets
    // 1회 실행으로 Phase 7에 필요한 BossData 에셋과 씬 UI를 자동 생성.
    // 웨이브/스테이지/스케일링 데이터는 Assets/Resources/Data/*.csv 로 관리됨.
    public static class SetupPhase7Assets
    {
        private const string EnemyDir  = "Assets/Data/Enemies";
        private const string StagePath = "Assets/Scenes/Stage_01.unity";

        private const string GuidEnemyC = "1e87419dddc6b854a9de8b91d5ead860"; // Brute (보스 베이스)

        [MenuItem("Vamsurlike/Phase 7/Setup Stage Assets")]
        public static void Run()
        {
            bool sceneWasOpened = false;
            if (!IsSceneLoaded(StagePath))
            {
                if (!System.IO.File.Exists(StagePath))
                {
                    Debug.LogError($"[SetupPhase7Assets] {StagePath} 를 찾을 수 없습니다.");
                    return;
                }
                EditorSceneManager.OpenScene(StagePath, OpenSceneMode.Single);
                sceneWasOpened = true;
                Debug.Log($"[SetupPhase7Assets] {StagePath} 씬을 자동으로 열었습니다.");
            }

            CreateBossData();
            CreateStageResultUI();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool saved = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            Debug.Log($"[SetupPhase7Assets] 완료 — 씬 저장={saved}");
            EditorUtility.DisplayDialog(
                "Phase 7 Setup 완료",
                "BossData_01.asset 생성 완료.\n\n" +
                "다음 작업:\n" +
                "• EnemySpawnManager Inspector → Enemy Registry에 BossData_01 등록\n" +
                "• Assets/Resources/Data/StageTable.csv → bossEnemyName=BossData_01 확인",
                "확인");
        }

        // ─── Boss EnemyDataSO ────────────────────────────────────────────

        private static void CreateBossData()
        {
            string path = $"{EnemyDir}/BossData_01.asset";
            if (AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path) != null)
            {
                Debug.Log("[SetupPhase7Assets] BossData_01.asset 이미 존재 — 건너뜀");
                return;
            }

            string sourcePath = AssetDatabase.GUIDToAssetPath(GuidEnemyC);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogError("[SetupPhase7Assets] Brute EnemyDataSO를 GUID로 찾을 수 없습니다.");
                return;
            }

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
        }

        // ─── StageResultUI 씬 오브젝트 ───────────────────────────────────

        private static void CreateStageResultUI()
        {
            var existing = Object.FindObjectsByType<StageResultUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing.Length > 0)
            {
                Debug.Log("[SetupPhase7Assets] StageResultUI 이미 존재 — 건너뜀");
                return;
            }

            var allCanvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allCanvas.Length == 0)
            {
                Debug.LogWarning("[SetupPhase7Assets] Canvas 없음 — StageResultUI 수동 배치 필요");
                return;
            }

            Canvas canvas = allCanvas[0];
            foreach (var c in allCanvas)
                if (c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }

            var rootGO   = new GameObject("StageResultRoot");
            rootGO.transform.SetParent(canvas.transform, false);
            var rootRect = rootGO.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var panelGO   = new GameObject("StageResultPanel");
            panelGO.transform.SetParent(rootGO.transform, false);
            var panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
            panelGO.SetActive(false);

            var textGO   = new GameObject("ResultText");
            textGO.transform.SetParent(panelGO.transform, false);
            var textRect  = textGO.AddComponent<RectTransform>();
            textRect.anchorMin        = new Vector2(0.5f, 0.5f);
            textRect.anchorMax        = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta        = new Vector2(600f, 150f);
            var tmp       = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = "RESULT";
            tmp.fontSize  = 72f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;

            var uiComp = rootGO.AddComponent<StageResultUI>();
            var uiSO   = new SerializedObject(uiComp);
            uiSO.FindProperty("resultPanel").objectReferenceValue = panelGO;
            uiSO.FindProperty("resultText").objectReferenceValue  = tmp;
            uiSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(uiComp);

            Debug.Log("[SetupPhase7Assets] StageResultRoot/Panel 생성 완료");
        }

        // ─── Utilities ───────────────────────────────────────────────────

        private static bool IsSceneLoaded(string scenePath)
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.path == scenePath && scene.isLoaded) return true;
            }
            return false;
        }
    }
}
