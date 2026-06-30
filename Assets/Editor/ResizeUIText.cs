using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ResizeUIText
{
    public static void Inspect()
    {
        string[] roots = { "UI/LevelUpCanvas", "UI/ChestRewardCanvas" };
        foreach (string root in roots)
        {
            var go = GameObject.Find(root);
            if (go == null) { Debug.LogWarning($"[ResizeUIText] 없음: {root}"); continue; }

            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
            Debug.Log($"=== {root} ({texts.Length}개 TMP) ===");
            foreach (var t in texts)
                Debug.Log($"  [{t.gameObject.name}] size={t.fontSize} autoSize={t.enableAutoSizing} path={GetPath(t.transform)}");
        }
    }

    public static void UpscaleText()
    {
        string[] roots = { "UI/LevelUpCanvas", "UI/ChestRewardCanvas" };
        int count = 0;

        foreach (string root in roots)
        {
            var go = GameObject.Find(root);
            if (go == null) continue;

            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
            foreach (var t in texts)
            {
                if (t.enableAutoSizing) continue;  // auto-size는 건드리지 않음

                float old = t.fontSize;
                float next = old < 20f  ? old * 2.2f :
                             old < 36f  ? old * 1.8f :
                             old * 1.4f;
                t.fontSize = Mathf.Round(next);
                EditorUtility.SetDirty(t);
                Debug.Log($"[ResizeUIText] {t.gameObject.name}: {old} → {t.fontSize}");
                count++;
            }
        }

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[ResizeUIText] 완료 — {count}개 TMP 텍스트 크기 확대.");
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
