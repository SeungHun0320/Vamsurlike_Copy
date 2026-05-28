using UnityEditor;
using UnityEngine;

public class SetupLayersTags
{
    public static string Execute()
    {
        var log = new System.Text.StringBuilder();

        // Enemy 프리팹: Layer 7 (Enemy), Tag "Enemy"
        string[] enemyPrefabs = {
            "Assets/Prefabs/Enemies/Enemy_A.prefab",
            "Assets/Prefabs/Enemies/Enemy B.prefab",
            "Assets/Prefabs/Enemies/Enemy C.prefab",
            "Assets/Prefabs/Enemies/Enemy D.prefab",
        };

        foreach (var path in enemyPrefabs)
            log.AppendLine(ApplyToAllChildren(path, 7, "Enemy"));

        // Projectile 프리팹: Layer 8 (Projectile), Tag "Projectile"
        log.AppendLine(ApplyToAllChildren("Assets/Prefabs/Skills/BasicProjectile.prefab", 8, "Projectile"));

        AssetDatabase.SaveAssets();
        log.AppendLine("Done.");
        return log.ToString();
    }

    private static string ApplyToAllChildren(string prefabPath, int layer, string tag)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return $"WARN: {prefabPath} not found.";

        using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
        var root = scope.prefabContentsRoot;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = layer;
            t.gameObject.tag   = tag;
        }

        return $"OK: {prefabPath} → layer={layer}, tag={tag}";
    }
}
