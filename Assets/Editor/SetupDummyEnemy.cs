using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Vamsurlike.Enemy;
using Vamsurlike.Data;

public class SetupDummyEnemy
{
    public static void Execute()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);

        // Create EnemyDataSO asset if it doesn't exist
        const string dataPath = "Assets/Data/Enemies/DummyEnemyData.asset";
        var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(dataPath);
        if (data == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Data/Enemies");
            data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.m_strEnemyName   = "Dummy";
            data.m_fHP            = 30f;
            data.m_fMoveSpeed     = 0f;
            data.m_fAttackPower   = 5f;
            data.m_fDefense       = 0f;
            data.m_fAttackRange   = 1.5f;
            data.m_fAttackInterval = 1f;
            data.m_iXPDrop        = 5;
            AssetDatabase.CreateAsset(data, dataPath);
            AssetDatabase.SaveAssets();
        }

        // Spawn a dummy enemy GameObject at (3, 0.5, 3)
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name  = "DummyEnemy";
        go.tag   = "Enemy";
        go.layer = LayerMask.NameToLayer("Enemy");
        go.transform.position   = new Vector3(3f, 0.5f, 3f);
        go.transform.localScale = new Vector3(0.8f, 1.8f, 0.8f);
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());

        var enemy = go.AddComponent<EnemyBase>();
        // Store data ref via a tiny helper component so Initialize is called at runtime
        var init = go.AddComponent<DummyEnemyInit>();
        init.m_data = data;

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupDummyEnemy] Dummy enemy placed at (3, 0.5, 3).");
    }
}
