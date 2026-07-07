using UnityEditor;
using UnityEngine;
using Vamsurlike.UI;

public static class ShowNetworkErrorDialog
{
    public static void Execute()
    {
        var dialog = Object.FindObjectOfType<NetworkErrorDialog>(true);
        if (dialog == null) { Debug.LogError("NetworkErrorDialog not found"); return; }

        // Show via reflection (panel is private)
        var panelField = typeof(NetworkErrorDialog)
            .GetField("panel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var msgField = typeof(NetworkErrorDialog)
            .GetField("messageText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (panelField != null)
        {
            var panel = panelField.GetValue(dialog) as GameObject;
            if (panel != null) panel.SetActive(true);
        }
        if (msgField != null)
        {
            var msg = msgField.GetValue(dialog) as TMPro.TextMeshProUGUI;
            if (msg != null) msg.text = "서버 연결이 끊어졌습니다.\n[디버그 미리보기]";
        }

        EditorUtility.SetDirty(dialog);
    }

    public static void Hide()
    {
        var dialog = Object.FindObjectOfType<NetworkErrorDialog>(true);
        if (dialog == null) return;

        var panelField = typeof(NetworkErrorDialog)
            .GetField("panel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (panelField != null)
        {
            var panel = panelField.GetValue(dialog) as GameObject;
            if (panel != null) panel.SetActive(false);
        }
    }
}
