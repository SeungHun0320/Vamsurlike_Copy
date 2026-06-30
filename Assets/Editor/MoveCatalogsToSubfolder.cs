using UnityEditor;
using UnityEngine;

public static class MoveCatalogsToSubfolder
{
    public static void Execute()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Catalogs"))
            AssetDatabase.CreateFolder("Assets/Resources", "Catalogs");

        string[] assets =
        {
            "Assets/Resources/UpgradeCatalog.asset",
            "Assets/Resources/CombineRecipeCatalog.asset",
            "Assets/Resources/ChestFallbackRewardCatalog.asset",
        };

        foreach (string src in assets)
        {
            string fileName = System.IO.Path.GetFileName(src);
            string dst      = "Assets/Resources/Catalogs/" + fileName;
            string error    = AssetDatabase.MoveAsset(src, dst);
            if (!string.IsNullOrEmpty(error))
                Debug.LogError($"Move failed [{src}]: {error}");
            else
                Debug.Log($"Moved: {dst}");
        }

        AssetDatabase.Refresh();
    }
}
