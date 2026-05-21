using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class PhaseZeroSetup
{
    public static void Execute()
    {
        SetupLayers();
        SetupTags();
        AssetDatabase.SaveAssets();
        Debug.Log("[PhaseZeroSetup] Layers and Tags setup complete.");
    }

    private static void SetupLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");

        // Layer 6~11
        var layerDefs = new Dictionary<int, string>
        {
            { 6,  "Player" },
            { 7,  "Enemy" },
            { 8,  "Projectile" },
            { 9,  "XPOrb" },
            { 10, "Item" },
            { 11, "Ground" },
        };

        foreach (var kv in layerDefs)
        {
            var prop = layers.GetArrayElementAtIndex(kv.Key);
            if (prop.stringValue != kv.Value)
            {
                prop.stringValue = kv.Value;
                Debug.Log($"[PhaseZeroSetup] Layer {kv.Key} = {kv.Value}");
            }
        }

        tagManager.ApplyModifiedProperties();
    }

    private static void SetupTags()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tagManager.FindProperty("tags");

        var requiredTags = new[] { "Player", "Enemy", "Boss", "Projectile", "XPOrb", "Item", "Ground" };

        foreach (var tag in requiredTags)
        {
            bool exists = false;
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
                Debug.Log($"[PhaseZeroSetup] Tag added: {tag}");
            }
        }

        tagManager.ApplyModifiedProperties();
    }
}
