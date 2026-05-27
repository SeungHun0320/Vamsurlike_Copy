using UnityEditor;
using UnityEditor.SceneManagement;

public class SaveStage01
{
    public static void Execute()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.Debug.Log($"[SaveStage01] 활성 씬 경로: {scene.path}");

        // 잘못 저장된 경우 올바른 경로로 이동
        string targetPath = "Assets/Scenes/Stage_01.unity";
        EditorSceneManager.SaveScene(scene, targetPath);
        UnityEngine.Debug.Log($"[SaveStage01] {targetPath} 저장 완료");
    }
}
