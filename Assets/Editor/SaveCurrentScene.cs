using UnityEditor;
using UnityEditor.SceneManagement;

public class SaveCurrentScene
{
    public static string Execute()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool saved = EditorSceneManager.SaveScene(scene, scene.path);
        return saved
            ? $"OK: Saved scene to '{scene.path}'"
            : $"ERROR: Failed to save scene '{scene.name}' at '{scene.path}'";
    }
}
