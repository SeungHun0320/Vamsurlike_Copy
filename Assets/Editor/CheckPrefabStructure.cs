using UnityEngine;
using UnityEditor;

public class CheckPrefabStructure
{
    public static void Execute()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/QuarterView 3D Action BE5/Prefabs/Player.prefab");
        if (prefab == null) { Debug.LogError("Prefab not found."); return; }

        LogHierarchy(prefab.transform, 0);
    }

    static void LogHierarchy(Transform t, int depth)
    {
        string indent = new string('-', depth * 2);
        var components = t.GetComponents<Component>();
        string comps = "";
        foreach (var c in components)
            if (c != null) comps += $" [{c.GetType().Name}]";

        Debug.Log($"{indent}{t.name}{comps}  pos={t.localPosition}");

        foreach (Transform child in t)
            LogHierarchy(child, depth + 1);
    }
}
