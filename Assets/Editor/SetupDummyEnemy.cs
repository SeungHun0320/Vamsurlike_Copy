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

        const string dataPath = "Assets/Data/Enemies/DummyEnemyData.asset";
        var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(dataPath);
        if (data == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Data/Enemies");
            data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyName     = "Dummy";
            data.hp            = 30f;
            data.moveSpeed     = 0f;
            data.attackPower   = 5f;
            data.defense       = 0f;
            data.attackRange   = 1.5f;
            data.attackInterval = 1f;
            data.xpDrop        = 5;
            AssetDatabase.CreateAsset(data, dataPath);
            AssetDatabase.SaveAssets();
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name  = "DummyEnemy";
        go.tag   = "Enemy";
        go.layer = LayerMask.NameToLayer("Enemy");
        go.transform.position   = new Vector3(3f, 0.5f, 3f);
        go.transform.localScale = new Vector3(0.8f, 1.8f, 0.8f);
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());

        go.AddComponent<EnemyBase>();
        var init = go.AddComponent<DummyEnemyInit>();
        init.enemyData = data;

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupDummyEnemy] Dummy enemy placed at (3, 0.5, 3).");
    }
}
