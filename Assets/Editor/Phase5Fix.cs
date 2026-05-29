using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public class Phase5Fix
{
    public static void Execute()
    {
        var old = GameObject.Find("LevelUpCanvas");
        if (old != null)
        {
            Object.DestroyImmediate(old);
            Debug.Log("[Phase5Fix] 기존 LevelUpCanvas 삭제");
        }

        Phase5Setup.Execute();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Phase5Fix] LevelUpCanvas 재생성 완료 — LevelUpUI를 Canvas에 배치");
    }

    public static void AddEventSystem()
    {
        // 이미 있으면 건너뜀 (DontDestroyOnLoad 포함)
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            Debug.Log("[Phase5Fix] EventSystem 이미 존재");
            return;
        }

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Phase5Fix] EventSystem 추가 완료");
    }
}
