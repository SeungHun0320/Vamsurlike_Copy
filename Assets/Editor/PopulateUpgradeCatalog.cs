using System.Linq;
using UnityEditor;
using UnityEngine;
using Vamsurlike.Upgrades;

public static class PopulateUpgradeCatalog
{
    public static void Execute()
    {
        const string catalogPath = "Assets/Resources/Catalogs/UpgradeCatalog.asset";

        var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(catalogPath);
        if (catalog == null) { Debug.LogError("UpgradeCatalog not found: " + catalogPath); return; }

        var guids   = AssetDatabase.FindAssets("t:UpgradeOptionSO", new[] { "Assets/Data/Upgrades" });
        var options = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<UpgradeOptionSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(o => o != null)
            .OrderBy(o => o.name)
            .ToArray();

        var so = new SerializedObject(catalog);
        var prop = so.FindProperty("options");
        prop.arraySize = options.Length;
        for (int i = 0; i < options.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = options[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        Debug.Log($"UpgradeCatalog populated: {options.Length} options\n"
            + string.Join("\n", options.Select((o, i) => $"  [{i}] {o.name}")));
    }
}
