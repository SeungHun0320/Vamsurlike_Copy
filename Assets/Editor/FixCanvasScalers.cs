using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FixCanvasScalers
{
    public static void Execute()
    {
        string[] targets = { "UI/LevelUpCanvas", "UI/ChestRewardCanvas", "UI/StageResultCanvas" };

        foreach (string path in targets)
        {
            var go = GameObject.Find(path);
            if (go == null) { Debug.LogWarning($"[FixCanvasScalers] Not found: {path}"); continue; }

            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null) { Debug.LogWarning($"[FixCanvasScalers] No CanvasScaler: {path}"); continue; }

            scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode   = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EditorUtility.SetDirty(go);
            Debug.Log($"[FixCanvasScalers] Fixed: {path}");
        }

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[FixCanvasScalers] 완료 — 3개 캔버스 Scale With Screen Size 1920×1080 적용");
    }
}
