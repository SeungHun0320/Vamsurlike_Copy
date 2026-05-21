using UnityEngine;
using UnityEditor;

public class VerifySetup
{
    public static void Execute()
    {
        // Verify Tags
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tagManager.FindProperty("tags");
        var sb = new System.Text.StringBuilder("[VerifySetup] Tags: ");
        for (int i = 0; i < tags.arraySize; i++)
            sb.Append(tags.GetArrayElementAtIndex(i).stringValue + " | ");
        Debug.Log(sb.ToString());

        // Verify Layers 6-11
        for (int i = 6; i <= 11; i++)
            Debug.Log($"[VerifySetup] Layer {i}: {LayerMask.LayerToName(i)}");
    }
}
