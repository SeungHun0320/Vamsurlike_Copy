using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PreviewCardUI
{
    public static void ShowLevelUp()
    {
        SetActive("UI/LevelUpCanvas/LevelUpPanel", true);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    public static void ShowChest()
    {
        SetActive("UI/ChestRewardCanvas/ChestPanel", true);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    public static void HideAll()
    {
        SetActive("UI/LevelUpCanvas/LevelUpPanel",       false);
        SetActive("UI/ChestRewardCanvas/ChestPanel", false);
        EditorSceneManager.SaveOpenScenes();
    }

    private static void SetActive(string path, bool active)
    {
        var go = GameObject.Find(path);
        if (go == null && !active) return;
        if (go == null) { Debug.LogWarning($"[PreviewCardUI] Not found: {path}"); return; }
        go.SetActive(active);
    }
}
