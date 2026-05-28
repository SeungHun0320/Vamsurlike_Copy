using UnityEditor;
using UnityEngine;

public class AddEnemyColliders
{
    private static readonly string[] EnemyPrefabPaths =
    {
        "Assets/Prefabs/Enemies/Enemy_A.prefab",
        "Assets/Prefabs/Enemies/Enemy B.prefab",
        "Assets/Prefabs/Enemies/Enemy C.prefab",
        "Assets/Prefabs/Enemies/Enemy D.prefab",
    };

    public static string Execute()
    {
        var log = new System.Text.StringBuilder();
        foreach (var path in EnemyPrefabPaths)
            log.AppendLine(AddCollider(path));
        AssetDatabase.SaveAssets();
        log.AppendLine("Done.");
        return log.ToString();
    }

    private static string AddCollider(string path)
    {
        using var scope = new PrefabUtility.EditPrefabContentsScope(path);
        var root = scope.prefabContentsRoot;

        if (root.GetComponent<CapsuleCollider>() != null)
            return $"SKIP: {path} 이미 CapsuleCollider 있음.";

        var col        = root.AddComponent<CapsuleCollider>();
        col.isTrigger  = true;
        col.height     = 1.8f;
        col.radius     = 0.5f;
        col.center     = new Vector3(0f, 0.9f, 0f);

        return $"OK: {path} → CapsuleCollider(Trigger) 추가";
    }
}
