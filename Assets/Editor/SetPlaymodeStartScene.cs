using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class SetPlaymodeStartScene
{
    private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

    static SetPlaymodeStartScene()
    {
        EditorApplication.delayCall += EnsureBootstrapStartScene;
    }

    [MenuItem("Vamsurlike/Set Bootstrap Play Mode Start Scene")]
    public static void Execute()
    {
        EnsureBootstrapStartScene();
    }

    private static void EnsureBootstrapStartScene()
    {
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
        if (scene == null)
        {
            UnityEngine.Debug.LogError($"[SetPlaymodeStartScene] Bootstrap 씬을 찾을 수 없습니다: {BootstrapScenePath}");
            return;
        }
        if (EditorSceneManager.playModeStartScene == scene) return;

        EditorSceneManager.playModeStartScene = scene;
        UnityEngine.Debug.Log($"[SetPlaymodeStartScene] Playmode start scene → {scene.name}");
    }
}
