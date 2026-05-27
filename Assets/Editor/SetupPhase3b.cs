using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
using Vamsurlike.Network;

public class SetupPhase3b
{
    public static void Execute()
    {
        // ── 1. XP Orb 비주얼 프리팹 생성 ─────────────────────────────────────
        string orbPrefabPath = "Assets/Prefabs/Items/XPOrb_Visual.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(orbPrefabPath) == null)
        {
            // 폴더 보장
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "XPOrb_Visual";
            orb.transform.localScale = Vector3.one * 0.3f;
            Object.DestroyImmediate(orb.GetComponent<SphereCollider>());

            // 노란색 머티리얼 — 기본 구체 머티리얼을 복사해서 색상만 변경 (셰이더 파이프라인 무관)
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "Materials");
            }
            var defaultMat = orb.GetComponent<MeshRenderer>().sharedMaterial;
            var mat = new Material(defaultMat);
            mat.color = new Color(1f, 0.9f, 0.1f);
            AssetDatabase.CreateAsset(mat, "Assets/Resources/Materials/XPOrb_Mat.mat");
            orb.GetComponent<MeshRenderer>().sharedMaterial = mat;

            PrefabUtility.SaveAsPrefabAsset(orb, orbPrefabPath);
            Object.DestroyImmediate(orb);
            Debug.Log($"[SetupPhase3b] XPOrb_Visual.prefab 생성: {orbPrefabPath}");
        }
        else
        {
            Debug.Log("[SetupPhase3b] XPOrb_Visual.prefab 이미 존재 — 스킵");
        }

        // ── 2. Stage_01: XPOrbManager.orbVisualPrefab 연결 + SynchronizeTransform 비활성화 ──
        var stageScene = EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Additive);
        var orbVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(orbPrefabPath);

        foreach (var root in stageScene.GetRootGameObjects())
        {
            // XPOrbManager 연결
            var xpMgr = root.GetComponent<Vamsurlike.Stage.XPOrbManager>();
            if (xpMgr != null)
            {
                var so = new SerializedObject(xpMgr);
                so.FindProperty("orbVisualPrefab").objectReferenceValue = orbVisualPrefab;
                so.ApplyModifiedProperties();

                // NetworkObject SynchronizeTransform 비활성화
                var netObj = root.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    var soNet = new SerializedObject(netObj);
                    soNet.FindProperty("SynchronizeTransform").boolValue = false;
                    soNet.ApplyModifiedProperties();
                }
                EditorUtility.SetDirty(xpMgr);
                Debug.Log("[SetupPhase3b] XPOrbManager 설정 완료");
            }

            // StageRuntime SynchronizeTransform 비활성화
            var stageRuntime = root.GetComponent<Vamsurlike.Stage.StageRuntime>();
            if (stageRuntime != null)
            {
                var netObj = root.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    var soNet = new SerializedObject(netObj);
                    soNet.FindProperty("SynchronizeTransform").boolValue = false;
                    soNet.ApplyModifiedProperties();
                }
                EditorUtility.SetDirty(stageRuntime);
                Debug.Log("[SetupPhase3b] StageRuntime SynchronizeTransform 비활성화");
            }
        }

        EditorSceneManager.SaveScene(stageScene, "Assets/Scenes/Stage_01.unity");
        EditorSceneManager.CloseScene(stageScene, true);

        // ── 3. Bootstrap: PoolManager networkConfigs[1] → Enemy_B ──────────
        var bootstrapScene = EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Additive);
        var enemyBPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy B.prefab");

        if (enemyBPrefab == null)
        {
            Debug.LogError("[SetupPhase3b] 'Enemy B.prefab'을 찾을 수 없습니다. Assets/Prefabs/Enemies/ 경로를 확인하세요.");
        }
        else
        {
            foreach (var root in bootstrapScene.GetRootGameObjects())
            {
                var pool = root.GetComponent<PoolManager>();
                if (pool == null) continue;

                var so = new SerializedObject(pool);
                var netArr = so.FindProperty("networkConfigs");

                // 현재 등록된 프리팹 목록 출력
                for (int i = 0; i < netArr.arraySize; i++)
                {
                    var elem = netArr.GetArrayElementAtIndex(i);
                    var prefabRef = elem.FindPropertyRelative("prefab").objectReferenceValue;
                    Debug.Log($"[SetupPhase3b] networkConfigs[{i}] = {(prefabRef != null ? prefabRef.name : "null")}");
                }

                // index 1에 Enemy_B 설정 (없으면 추가)
                if (netArr.arraySize < 2) netArr.arraySize = 2;
                var entry = netArr.GetArrayElementAtIndex(1);
                entry.FindPropertyRelative("prefab").objectReferenceValue = enemyBPrefab;
                entry.FindPropertyRelative("warmupCount").intValue = 20;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(pool);
                Debug.Log("[SetupPhase3b] PoolManager networkConfigs[1] → Enemy_B 설정 완료");
                break;
            }
        }

        EditorSceneManager.SaveScene(bootstrapScene, "Assets/Scenes/Bootstrap.unity");
        EditorSceneManager.CloseScene(bootstrapScene, true);

        AssetDatabase.Refresh();
        Debug.Log("[SetupPhase3b] 모든 설정 완료!");
    }
}
