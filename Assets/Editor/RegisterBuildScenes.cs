using UnityEditor;
using System.Linq;

public static class RegisterBuildScenes
{
    public static void Execute()
    {
        var scenePaths = new[]
        {
            "Assets/Scenes/Bootstrap.unity",
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Stage_01.unity",
        };

        var existing = EditorBuildSettings.scenes
            .Where(s => !scenePaths.Contains(s.path))
            .ToList();

        var toAdd = scenePaths
            .Select(p => new EditorBuildSettingsScene(p, true))
            .ToList();

        EditorBuildSettings.scenes = toAdd.Concat(existing).ToArray();

        UnityEngine.Debug.Log("[BuildSettings] Scenes registered: " + string.Join(", ", scenePaths));
    }
}
