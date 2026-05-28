using UnityEditor;
using UnityEngine;
using Vamsurlike.Data;

public class SetupSkillManagerCharacterData
{
    public static string Execute()
    {
        var log = new System.Text.StringBuilder();

        const string playerPrefabPath   = "Assets/Prefabs/Player/NetworkedPlayer.prefab";
        const string characterDataPath  = "Assets/Data/Player/PlayerData.asset";

        string[] skillPaths =
        {
            "Assets/Data/Skills/SkillData_BasicProjectile.asset",
            "Assets/Data/Skills/SkillData_DamageAura.asset",
            "Assets/Data/Skills/SkillData_Orbital.asset",
            "Assets/Data/Skills/SkillData_PierceProjectile.asset",
            "Assets/Data/Skills/SkillData_SpreadProjectile.asset",
            "Assets/Data/Skills/SkillData_BulletStorm.asset",
        };

        // ── 1. CharacterDataSO.startingSkills 세팅 ───────────────
        var characterData = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(characterDataPath);
        if (characterData == null)
            return $"ERROR: CharacterDataSO not found at {characterDataPath}";

        var charSO         = new SerializedObject(characterData);
        var skillsProp     = charSO.FindProperty("startingSkills");
        skillsProp.arraySize = skillPaths.Length;
        for (int i = 0; i < skillPaths.Length; i++)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(skillPaths[i]);
            skillsProp.GetArrayElementAtIndex(i).objectReferenceValue = asset;
            if (asset == null) log.AppendLine($"WARN: skill not found: {skillPaths[i]}");
        }
        charSO.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(characterData);
        log.AppendLine($"OK: CharacterDataSO.startingSkills = {skillPaths.Length} skills");

        // ── 2. SkillManager.characterData 연결 ──────────────────
        using (var scope = new PrefabUtility.EditPrefabContentsScope(playerPrefabPath))
        {
            var root     = scope.prefabContentsRoot;
            var mgrComp  = root.GetComponent("Vamsurlike.Skills.SkillManager");
            if (mgrComp == null) return "ERROR: SkillManager not found on prefab.";

            var mgrSO    = new SerializedObject(mgrComp);
            var dataProp = mgrSO.FindProperty("characterData");
            if (dataProp == null) return "ERROR: characterData property not found.";

            dataProp.objectReferenceValue = characterData;
            mgrSO.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("OK: SkillManager.characterData = PlayerData.asset");
        }

        AssetDatabase.SaveAssets();
        log.AppendLine("Done.");
        return log.ToString();
    }
}
