using UnityEditor;
using UnityEditor.SceneManagement;

public class SaveCurrentScene
{
    public static void Execute()
    {
        EditorSceneManager.SaveOpenScenes();
    }
}
