using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// save_scene MCP 툴의 scene_name 파라미터가 기존 씬 경로가 아니라 Assets/ 바로 아래
// 새 경로로 해석되어(Assets/Stage_01.unity 중복 생성) 실제 씬(Assets/Scenes/Stage_01.unity)에는
// 저장이 안 되는 문제를 우회하기 위한 일회성 스크립트 — 현재 열려 있는 씬을 원래 경로 그대로 저장한다.
public static class SaveActiveScene
{
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        bool ok = EditorSceneManager.SaveScene(scene, scene.path);
        Debug.Log($"[Build] SaveActiveScene: path={scene.path} success={ok}");
    }
}
