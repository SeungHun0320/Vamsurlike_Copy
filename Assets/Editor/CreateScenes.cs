using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CreateScenes
{
    public static void Execute()
    {
        CreateScene("Bootstrap");
        CreateScene("MainMenu");
        CreateScene("Stage_01");
        AddScenesToBuildSettings();
        Debug.Log("[CreateScenes] All scenes created and registered.");
    }

    private static void CreateScene(string name)
    {
        string path = $"Assets/Scenes/{name}.unity";
        if (System.IO.File.Exists($"Assets/Scenes/{name}.unity"))
        {
            Debug.Log($"[CreateScenes] {name} already exists, skipping.");
            return;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[CreateScenes] Created: {path}");
    }

    private static void AddScenesToBuildSettings()
    {
        var scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Bootstrap.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Stage_01.unity",  true),
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("[CreateScenes] Build Settings updated.");
    }
}
