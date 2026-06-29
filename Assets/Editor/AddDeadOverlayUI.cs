using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI;

// 사망(2단계 DeadWaiting) 전체화면 회색 오버레이를 HUDCanvas에 추가한다.
// 기존 UI 위치는 건드리지 않는다.
public static class AddDeadOverlayUI
{
    const string SCENE_PATH = "Assets/Scenes/Stage_01.unity";

    [MenuItem("Vamsurlike/Add Dead Overlay UI")]
    public static void Execute()
    {
        var scene = EditorSceneManager.GetSceneByPath(SCENE_PATH);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[AddDeadOverlayUI] Stage_01 씬이 열려있지 않습니다.");
            return;
        }

        GameObject hudCanvas = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            hudCanvas = FindDeep(root.transform, "HUDCanvas");
            if (hudCanvas != null) break;
        }
        if (hudCanvas == null) { Debug.LogError("[AddDeadOverlayUI] HUDCanvas not found"); return; }

        var playerHUDUI = hudCanvas.GetComponent<PlayerHUDUI>();
        if (playerHUDUI == null) { Debug.LogError("[AddDeadOverlayUI] PlayerHUDUI not found"); return; }

        // ─── Dead Overlay (전체화면 회색) ────────────────────────────
        RemoveIfExists(hudCanvas, "DeadOverlay");
        var deadGO = new GameObject("DeadOverlay");
        deadGO.transform.SetParent(hudCanvas.transform, false);
        deadGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.75f);
        SetRect(deadGO, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // 사망 메시지
        var msgGO = MakeTMP(deadGO, "Message", 42, TextAlignmentOptions.Center);
        var msg   = msgGO.GetComponent<TextMeshProUGUI>();
        msg.text      = "사망";
        msg.color     = new Color(0.85f, 0.85f, 0.85f, 1f);
        msg.fontStyle = FontStyles.Bold;
        SetRect(msgGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(400, 80));

        // 부활 카운트다운
        var timerGO = MakeTMP(deadGO, "DeadTimer", 36, TextAlignmentOptions.Center);
        var timer   = timerGO.GetComponent<TextMeshProUGUI>();
        timer.color     = new Color(0.7f, 0.7f, 0.7f, 1f);
        timer.fontStyle = FontStyles.Bold;
        SetRect(timerGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(400, 60));

        deadGO.SetActive(false);

        ConnectField(playerHUDUI, "deadOverlay",   deadGO);
        ConnectField(playerHUDUI, "deadTimerText", timer);

        EditorUtility.SetDirty(hudCanvas);
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        Debug.Log("[AddDeadOverlayUI] Done — DeadOverlay 추가 완료");
    }

    static GameObject FindDeep(Transform t, string name)
    {
        if (t.name == name) return t.gameObject;
        foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
        return null;
    }

    static void RemoveIfExists(GameObject parent, string name)
    {
        var existing = parent.transform.Find(name);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
    }

    static GameObject MakeTMP(GameObject parent, string name, float size, TextAlignmentOptions align)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize  = size;
        tmp.alignment = align;
        tmp.color     = Color.white;
        return go;
    }

    static void SetRect(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void ConnectField(Component target, string fieldName, Object value)
    {
        var so   = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null) { Debug.LogWarning("[AddDeadOverlayUI] Field not found: " + fieldName); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
