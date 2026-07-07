using UnityEditor;
using UnityEngine;

public static class ShowSettingsPanel
{
    public static void Execute()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return;
        var panel = canvas.transform.Find("SettingsPanel");
        if (panel != null) panel.gameObject.SetActive(true);
    }

    public static void Hide()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return;
        var panel = canvas.transform.Find("SettingsPanel");
        if (panel != null) panel.gameObject.SetActive(false);
    }
}

