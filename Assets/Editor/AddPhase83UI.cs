using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI;

// Phase 8.3 UI 요소를 기존 HUDCanvas에 추가하는 에디터 스크립트.
// 기존 UI 위치는 전혀 건드리지 않고, 신규 오브젝트만 삽입·저장한다.
public static class AddPhase83UI
{
    const string SCENE_PATH = "Assets/Scenes/Stage_01.unity";

    [MenuItem("Vamsurlike/Add Phase 8.3 UI")]
    public static void Execute()
    {
        var scene = EditorSceneManager.GetSceneByPath(SCENE_PATH);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[AddPhase83UI] Stage_01 씬이 열려있지 않습니다.");
            return;
        }

        // HUDCanvas 탐색
        GameObject hudCanvas = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            hudCanvas = FindDeep(root.transform, "HUDCanvas");
            if (hudCanvas != null) break;
        }
        if (hudCanvas == null) { Debug.LogError("[AddPhase83UI] HUDCanvas not found"); return; }

        var playerHUDUI = hudCanvas.GetComponent<PlayerHUDUI>();
        if (playerHUDUI == null) { Debug.LogError("[AddPhase83UI] PlayerHUDUI not found"); return; }

        // ─── 1. Downed Overlay (전체화면 반투명 적색 오버레이) ─────────
        RemoveIfExists(hudCanvas, "DownedOverlay");
        var downedGO = new GameObject("DownedOverlay");
        downedGO.transform.SetParent(hudCanvas.transform, false);
        downedGO.AddComponent<Image>().color = new Color(0.5f, 0f, 0f, 0.5f);
        SetRect(downedGO, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var msgGO = MakeTMP(downedGO, "Message", 36, TextAlignmentOptions.Center);
        msgGO.GetComponent<TextMeshProUGUI>().text =
            "다운됨\n<size=22>팀원이 부활시켜 줄 때까지 기다리세요</size>";
        SetRect(msgGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(700, 100));

        var timerGO = MakeTMP(downedGO, "DownedTimer", 48, TextAlignmentOptions.Center);
        timerGO.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.35f, 0.35f, 1f);
        SetRect(timerGO, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(300, 80));

        downedGO.SetActive(false);
        ConnectField(playerHUDUI, "downedOverlay",   downedGO);
        ConnectField(playerHUDUI, "downedTimerText", timerGO.GetComponent<TextMeshProUGUI>());

        // ─── 2. Co-op HUD (우측 상단, 타이머 아래) ────────────────────
        // 타이머: anchor(1,1), pos(-20,-20), size(300,80) → 그 아래: y=-110
        RemoveIfExists(hudCanvas, "CoopHUD");
        var coopGO = new GameObject("CoopHUD");
        coopGO.transform.SetParent(hudCanvas.transform, false);
        // Image 먼저 추가해야 RectTransform이 자동으로 붙음 (모노비헤이비어만 추가하면 안 붙음)
        var coopBg = coopGO.AddComponent<Image>();
        coopBg.color = Color.clear;
        coopBg.raycastTarget = false;
        coopGO.AddComponent<CoopHUDUI>();
        SetRect(coopGO, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-20, -110), new Vector2(240, 300));

        // ─── 3. Revive Progress Bar (하단 중앙, XP 바 위) ─────────────
        // XP 바: anchor(0.5,0), pos(0,20), size(1200,40) → 그 위(y=80)에 배치
        RemoveIfExists(hudCanvas, "ReviveProgressPanel");
        var revGO = new GameObject("ReviveProgressPanel");
        revGO.transform.SetParent(hudCanvas.transform, false);
        var revUI = revGO.AddComponent<ReviveProgressUI>();
        revGO.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        SetRect(revGO, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(500, 52));

        var revFillGO = new GameObject("Fill");
        revFillGO.transform.SetParent(revGO.transform, false);
        var revFill = revFillGO.AddComponent<Image>();
        revFill.color      = new Color(0.2f, 0.8f, 0.4f, 1f);
        revFill.type       = Image.Type.Filled;
        revFill.fillMethod = Image.FillMethod.Horizontal;
        revFill.fillOrigin = 0;
        revFill.fillAmount = 0f;
        SetRect(revFillGO, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

        var revTxtGO = MakeTMP(revGO, "Text", 22, TextAlignmentOptions.Center);
        revTxtGO.GetComponent<TextMeshProUGUI>().text = "부활 중...";
        SetRect(revTxtGO, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        ConnectField(revUI, "panel",        revGO);
        ConnectField(revUI, "progressFill", revFill);
        ConnectField(revUI, "progressText", revTxtGO.GetComponent<TextMeshProUGUI>());

        EditorUtility.SetDirty(hudCanvas);
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        Debug.Log("[AddPhase83UI] Done — Co-op HUD / DownedOverlay / ReviveProgressBar 추가 완료");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

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
        // ?? 연산자는 Unity fake-null을 non-null로 보기 때문에 명시적 null 체크 사용
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void ConnectField(Component target, string fieldName, Object value)
    {
        var so   = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null) { Debug.LogWarning("[AddPhase83UI] Field not found: " + fieldName); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
