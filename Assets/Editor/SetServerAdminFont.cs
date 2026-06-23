using UnityEngine;
using UnityEditor;
using TMPro;

public class SetServerAdminFont
{
    public static void Execute()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Resources/Fonts/MalgunGothic SDF.asset");
        if (fontAsset == null) { Debug.LogError("Font not found"); return; }

        var paths = new[]
        {
            "Canvas/ServerAdminPanel/PlayerCountText",
            "Canvas/ServerAdminPanel/StartGameButton/Text"
        };

        var root = GameObject.Find("Canvas");
        if (root == null) { Debug.LogError("Canvas not found"); return; }

        foreach (var path in paths)
        {
            var t = root.transform.Find(path.Replace("Canvas/", ""));
            if (t == null) continue;
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp == null) continue;
            tmp.font = fontAsset;
            tmp.color = Color.white;
            tmp.fontSize = 24;
            EditorUtility.SetDirty(tmp);
        }
    }
}
