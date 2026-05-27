using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vamsurlike.Network;
using Vamsurlike.Stage;
using Vamsurlike.Data;

public class SetupPhase3
{
    public static void Execute()
    {
        // ── 1. Bootstrap: PoolManager networkConfigs 설정 ──────────────────
        var bootstrapScene = EditorSceneManager.GetSceneByPath("Assets/Scenes/Bootstrap.unity");
        if (!bootstrapScene.IsValid())
            bootstrapScene = EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Single);

        var enemyPrefabs = new[]
        {
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy_A.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy B.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy C.prefab")
        };

        foreach (var enemyPrefab in enemyPrefabs)
        {
            if (enemyPrefab == null)
            {
                Debug.LogError("[SetupPhase3] 적 프리팹 중 누락된 에셋이 있습니다.");
                return;
            }
        }

        foreach (var root in bootstrapScene.GetRootGameObjects())
        {
            var pool = root.GetComponent<PoolManager>();
            if (pool == null) continue;

            var so = new SerializedObject(pool);
            var netArr = so.FindProperty("networkConfigs");
            netArr.arraySize = enemyPrefabs.Length;
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                var elem = netArr.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("prefab").objectReferenceValue = enemyPrefabs[i];
                elem.FindPropertyRelative("warmupCount").intValue = 20;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(pool);
            Debug.Log("[SetupPhase3] PoolManager networkConfigs 설정 완료");
            break;
        }
        EditorSceneManager.SaveScene(bootstrapScene);

        // ── 2. WaveDataSO 에셋 생성 ────────────────────────────────────────
        var waveData = ScriptableObject.CreateInstance<WaveDataSO>();
        waveData.waveDuration = 20f;

        var dataA = AssetDatabase.LoadAssetAtPath<EnemyDataSO>("Assets/Data/Enemies/EnemyData_A.asset");
        var dataB = AssetDatabase.LoadAssetAtPath<EnemyDataSO>("Assets/Data/Enemies/EnemyData_B.asset");
        var dataC = AssetDatabase.LoadAssetAtPath<EnemyDataSO>("Assets/Data/Enemies/EnemyData_C.asset");

        var soWave = new SerializedObject(waveData);
        var entries = soWave.FindProperty("entries");
        entries.arraySize = 3;

        void SetEntry(int idx, EnemyDataSO data, int count, float interval)
        {
            var e = entries.GetArrayElementAtIndex(idx);
            e.FindPropertyRelative("enemyData").objectReferenceValue = data;
            e.FindPropertyRelative("count").intValue = count;
            e.FindPropertyRelative("spawnInterval").floatValue = interval;
        }

        SetEntry(0, dataA, 5,  0.6f);
        SetEntry(1, dataB, 4,  0.4f);
        SetEntry(2, dataC, 2,  1.0f);
        soWave.ApplyModifiedProperties();

        AssetDatabase.CreateAsset(waveData, "Assets/Data/Stages/WaveData_01.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupPhase3] WaveData_01.asset 생성 완료");

        // ── 3. Stage_01: WaveController 배치 ──────────────────────────────
        var stageScene = EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Additive);

        // 이미 있으면 스킵
        bool exists = false;
        foreach (var root in stageScene.GetRootGameObjects())
            if (root.GetComponent<WaveController>() != null) { exists = true; break; }

        if (!exists)
        {
            var go = new GameObject("WaveController");
            SceneManager.MoveGameObjectToScene(go, stageScene);
            var wc = go.AddComponent<WaveController>();

            var soWC = new SerializedObject(wc);
            var wavesArr = soWC.FindProperty("waves");
            wavesArr.arraySize = 1;
            wavesArr.GetArrayElementAtIndex(0).objectReferenceValue = waveData;
            soWC.FindProperty("spawnRadius").floatValue = 15f;
            soWC.FindProperty("loopLastWave").boolValue = true;
            soWC.ApplyModifiedProperties();
            EditorUtility.SetDirty(wc);
            Debug.Log("[SetupPhase3] WaveController 배치 완료");
        }
        else
        {
            Debug.Log("[SetupPhase3] WaveController 이미 존재 — 스킵");
        }

        EditorSceneManager.SaveScene(stageScene);
        EditorSceneManager.CloseScene(stageScene, true);

        AssetDatabase.Refresh();
        Debug.Log("[SetupPhase3] 모든 설정 완료!");
    }
}
