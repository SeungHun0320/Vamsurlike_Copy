using UnityEngine;
using UnityEditor;
using Vamsurlike.Data;

public class CreateCharacterData
{
    public static void Execute()
    {
        var data = ScriptableObject.CreateInstance<CharacterDataSO>();
        data.characterName   = "Player";
        data.baseHP          = 100f;
        data.baseMoveSpeed   = 7f;
        data.baseAttackPower = 10f;
        data.baseDefense     = 0f;
        data.basePickupRadius = 2f;

        const string dir = "Assets/Data/Player";
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        AssetDatabase.CreateAsset(data, $"{dir}/PlayerData.asset");
        AssetDatabase.SaveAssets();
        Debug.Log($"[CreateCharacterData] Created PlayerData.asset  MoveSpeed={data.baseMoveSpeed}");
    }
}
