using UnityEditor;
using UnityEngine;

public static class MoveCircleSprite
{
    public static void Execute()
    {
        // Resources/Sprites/UI 폴더 생성
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Sprites"))
            AssetDatabase.CreateFolder("Assets/Resources", "Sprites");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Sprites/UI"))
            AssetDatabase.CreateFolder("Assets/Resources/Sprites", "UI");

        string error = AssetDatabase.MoveAsset(
            "Assets/Sprites/UI/Circle.png",
            "Assets/Resources/Sprites/UI/Circle.png");

        if (!string.IsNullOrEmpty(error))
            Debug.LogError("Move failed: " + error);
        else
            Debug.Log("Moved to Assets/Resources/Sprites/UI/Circle.png");
    }
}
