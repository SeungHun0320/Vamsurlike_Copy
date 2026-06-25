using UnityEditor;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Editor
{
    // Menu: Vamsurlike > Phase 7 > Setup Boss Missile Prefab
    // BossData_01.asset 의 bossMissilePrefab 필드에 "Missile Boss" 프리팹을 일괄 할당한다.
    public static class SetupBossMissilePrefab
    {
        private const string MissilePrefabPath = "Assets/Prefabs/Enemies/Missile Boss.prefab";

        [MenuItem("Vamsurlike/Phase 7/Setup Boss Missile Prefab")]
        public static void Run()
        {
            var missile = AssetDatabase.LoadAssetAtPath<GameObject>(MissilePrefabPath);
            if (missile == null)
            {
                Debug.LogError($"[SetupBossMissilePrefab] 프리팹 없음: {MissilePrefabPath}");
                return;
            }

            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDataSO", new[] { "Assets/Data" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
                if (data == null || !data.isBoss) continue;

                var so   = new SerializedObject(data);
                var prop = so.FindProperty("bossMissilePrefab");
                if (prop == null) continue;

                prop.objectReferenceValue = missile;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(data);
                count++;
                Debug.Log($"[SetupBossMissilePrefab] '{path}' → bossMissilePrefab 할당");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupBossMissilePrefab] 완료 — {count}개 EnemyDataSO 갱신");
        }
    }
}
