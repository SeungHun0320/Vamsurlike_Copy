using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vamsurlike.UI;

// HUD의 ReviveProgressPanel 제거 + NetworkedPlayer 프리팹에 WorldReviveProgressUI 추가
public static class SetupWorldReviveUI
{
    const string SCENE_PATH  = "Assets/Scenes/Stage_01.unity";
    const string PLAYER_PREFAB = "Assets/Prefabs/Player/NetworkedPlayer.prefab";

    [MenuItem("Vamsurlike/Setup World Revive UI")]
    public static void Execute()
    {
        RemoveHUDPanel();
        AddToPlayerPrefab();
    }

    static void RemoveHUDPanel()
    {
        var scene = EditorSceneManager.GetSceneByPath(SCENE_PATH);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[SetupWorldReviveUI] Stage_01 씬이 열려있지 않습니다.");
            return;
        }

        GameObject hudCanvas = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            hudCanvas = FindDeep(root.transform, "HUDCanvas");
            if (hudCanvas != null) break;
        }

        if (hudCanvas == null) { Debug.LogWarning("[SetupWorldReviveUI] HUDCanvas not found"); return; }

        var panel = hudCanvas.transform.Find("ReviveProgressPanel");
        if (panel != null)
        {
            Object.DestroyImmediate(panel.gameObject);
            EditorUtility.SetDirty(hudCanvas);
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            Debug.Log("[SetupWorldReviveUI] ReviveProgressPanel removed from HUDCanvas");
        }
        else
        {
            Debug.Log("[SetupWorldReviveUI] ReviveProgressPanel already absent");
        }
    }

    static void AddToPlayerPrefab()
    {
        using var scope = new PrefabUtility.EditPrefabContentsScope(PLAYER_PREFAB);
        var root = scope.prefabContentsRoot;

        if (root.GetComponent<WorldReviveProgressUI>() != null)
        {
            Debug.Log("[SetupWorldReviveUI] WorldReviveProgressUI already exists on NetworkedPlayer");
            return;
        }

        root.AddComponent<WorldReviveProgressUI>();
        Debug.Log("[SetupWorldReviveUI] WorldReviveProgressUI added to NetworkedPlayer.prefab");
    }

    static GameObject FindDeep(Transform t, string name)
    {
        if (t.name == name) return t.gameObject;
        foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
        return null;
    }
}
