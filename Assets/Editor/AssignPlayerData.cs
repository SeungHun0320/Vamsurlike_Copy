using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Vamsurlike.Player;
using Vamsurlike.Data;

public class AssignPlayerData
{
    public static void Execute()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);

        var playerGo = GameObject.Find("Player");
        if (playerGo == null) { Debug.LogError("[AssignPlayerData] Player not found."); return; }

        var stats = playerGo.GetComponent<PlayerStats>();
        if (stats == null) { Debug.LogError("[AssignPlayerData] PlayerStats not found."); return; }

        var data = AssetDatabase.LoadAssetAtPath<CharacterDataSO>("Assets/Data/Player/PlayerData.asset");
        if (data == null) { Debug.LogError("[AssignPlayerData] PlayerData.asset not found."); return; }

        var so = new SerializedObject(stats);
        so.FindProperty("characterData").objectReferenceValue = data;
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[AssignPlayerData] PlayerData assigned. MoveSpeed={data.baseMoveSpeed}");
    }
}
