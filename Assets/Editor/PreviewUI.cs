using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PreviewUI
{
    // STAGE CLEAR 상태 프리뷰: 타이틀 패널 + 결과 버튼만 표시
    public static void ShowClearBanner()
    {
        SetActive("UI/StageResultCanvas/StageResultRoot/Overlay",         true);
        SetActive("UI/StageResultCanvas/StageResultRoot/StageResultPanel",true);
        SetActive("UI/StageResultCanvas/StageResultRoot/ContinueButton",  true);
        SetActive("UI/StageResultCanvas/StageResultRoot/StatsPanel",      false);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    // RESULT 상태 프리뷰: 타이틀 패널 + 통계 패널 (버튼 없음)
    public static void ShowResult()
    {
        SetActive("UI/StageResultCanvas/StageResultRoot/Overlay",         true);
        SetActive("UI/StageResultCanvas/StageResultRoot/StageResultPanel",true);
        SetActive("UI/StageResultCanvas/StageResultRoot/ContinueButton",  false);
        SetActive("UI/StageResultCanvas/StageResultRoot/StatsPanel",      true);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    public static void HideResult()
    {
        SetActive("UI/StageResultCanvas/StageResultRoot/Overlay",         false);
        SetActive("UI/StageResultCanvas/StageResultRoot/StageResultPanel",false);
        SetActive("UI/StageResultCanvas/StageResultRoot/ContinueButton",  false);
        SetActive("UI/StageResultCanvas/StageResultRoot/StatsPanel",      false);
        EditorSceneManager.SaveOpenScenes();
    }

    private static void SetActive(string path, bool active)
    {
        var go = GameObject.Find(path);
        if (go == null && !active) return;
        if (go == null) { Debug.LogWarning($"[PreviewUI] Not found: {path}"); return; }
        go.SetActive(active);
    }
}
