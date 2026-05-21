using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SetupBootstrap
{
    public static void Execute()
    {
        // Bootstrap 씬 열기
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Single);

        // GameInstance 오브젝트 생성 + BootstrapLoader 추가
        var go = new GameObject("GameInstance");
        go.AddComponent<Vamsurlike.Core.BootstrapLoader>();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupBootstrap] Bootstrap scene configured and saved.");
    }
}
