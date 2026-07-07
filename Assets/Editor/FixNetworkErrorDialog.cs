using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI;

public static class FixNetworkErrorDialog
{
    public static void Execute()
    {
        var canvas = GameObject.Find("LoadingScreenCanvas");
        if (canvas == null) { Debug.LogError("[Fix] LoadingScreenCanvas not found"); return; }

        var existing = canvas.transform.Find("NetworkErrorDialog");
        if (existing == null) { Debug.LogError("[Fix] NetworkErrorDialog not found"); return; }

        // Transform이면 RectTransform으로 교체 필요
        if (existing.GetComponent<RectTransform>() != null)
        {
            Debug.Log("[Fix] already RectTransform — fixing Panel position only");
            FixPanelPosition(existing.gameObject);
            EditorUtility.SetDirty(canvas);
            return;
        }

        int siblingIndex = existing.GetSiblingIndex();

        // typeof(RectTransform) 전달 → Transform 슬롯이 RectTransform으로 생성됨
        var newGO = new GameObject("NetworkErrorDialog", typeof(RectTransform));
        newGO.transform.SetParent(canvas.transform, false);

        var rt = (RectTransform)newGO.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // 자식 이동
        while (existing.childCount > 0)
            existing.GetChild(0).SetParent(newGO.transform, false);

        // 컴포넌트 재연결
        var newComp = newGO.AddComponent<NetworkErrorDialog>();
        var panel   = newGO.transform.Find("Panel");
        if (panel != null)
        {
            FixPanelPosition(newGO);

            var so = new SerializedObject(newComp);
            so.FindProperty("panel").objectReferenceValue          = panel.gameObject;
            var msg = panel.Find("MessageText");
            if (msg  != null) so.FindProperty("messageText").objectReferenceValue    = msg.GetComponent<TextMeshProUGUI>();
            var btn  = panel.Find("ConfirmButton");
            if (btn  != null) so.FindProperty("confirmButton").objectReferenceValue  = btn.GetComponent<Button>();
            so.ApplyModifiedProperties();
        }

        Object.DestroyImmediate(existing.gameObject);
        newGO.transform.SetSiblingIndex(siblingIndex);

        EditorUtility.SetDirty(canvas);
        Debug.Log("[Fix] NetworkErrorDialog rebuilt with RectTransform");
    }

    private static void FixPanelPosition(GameObject dialogGO)
    {
        var panel = dialogGO.transform.Find("Panel");
        if (panel == null) return;
        var pr = panel.GetComponent<RectTransform>();
        if (pr == null) return;
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
        pr.anchoredPosition = Vector2.zero;
        pr.sizeDelta        = new Vector2(700f, 320f);
    }
}
