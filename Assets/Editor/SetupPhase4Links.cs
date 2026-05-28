using UnityEditor;
using UnityEngine;

public class SetupPhase4Links
{
    public static string Execute()
    {
        var log = new System.Text.StringBuilder();

        const string playerPrefabPath  = "Assets/Prefabs/Player/NetworkedPlayer.prefab";
        const string orbitalVisualPath = "Assets/Prefabs/VFX/OrbitalVisual.prefab";

        string[] skillPaths =
        {
            "Assets/Data/Skills/SkillData_BasicProjectile.asset",
            "Assets/Data/Skills/SkillData_DamageAura.asset",
            "Assets/Data/Skills/SkillData_Orbital.asset",
            "Assets/Data/Skills/SkillData_PierceProjectile.asset",
            "Assets/Data/Skills/SkillData_SpreadProjectile.asset",
        };

        var orbitalVisual = AssetDatabase.LoadAssetAtPath<GameObject>(orbitalVisualPath);
        if (orbitalVisual == null) return "ERROR: OrbitalVisual prefab not found at " + orbitalVisualPath;

        using (var scope = new PrefabUtility.EditPrefabContentsScope(playerPrefabPath))
        {
            var root = scope.prefabContentsRoot;
            var so   = new SerializedObject(root.GetComponents<MonoBehaviour>()[0]);

            // OrbitalNetworkSkill → orbitalVisualPrefab
            var orbitalComp = root.GetComponent("Vamsurlike.Skills.OrbitalNetworkSkill");
            if (orbitalComp != null)
            {
                var orbSO   = new SerializedObject(orbitalComp);
                var orbProp = orbSO.FindProperty("orbitalVisualPrefab");
                if (orbProp != null)
                {
                    orbProp.objectReferenceValue = orbitalVisual;
                    orbSO.ApplyModifiedPropertiesWithoutUndo();
                    log.AppendLine("OK: OrbitalNetworkSkill.orbitalVisualPrefab = OrbitalVisual");
                }
                else log.AppendLine("WARN: orbitalVisualPrefab property not found.");
            }
            else log.AppendLine("WARN: OrbitalNetworkSkill component not found.");

            // SkillManager → startingSkills
            var skillMgrComp = root.GetComponent("Vamsurlike.Skills.SkillManager");
            if (skillMgrComp != null)
            {
                var mgrSO        = new SerializedObject(skillMgrComp);
                var skillsProp   = mgrSO.FindProperty("startingSkills");
                if (skillsProp != null)
                {
                    skillsProp.arraySize = skillPaths.Length;
                    for (int i = 0; i < skillPaths.Length; i++)
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(skillPaths[i]);
                        skillsProp.GetArrayElementAtIndex(i).objectReferenceValue = asset;
                        if (asset == null) log.AppendLine($"WARN: skill asset not found: {skillPaths[i]}");
                    }
                    mgrSO.ApplyModifiedPropertiesWithoutUndo();
                    log.AppendLine($"OK: SkillManager.startingSkills = {skillPaths.Length} skills");
                }
                else log.AppendLine("WARN: startingSkills property not found.");
            }
            else log.AppendLine("WARN: SkillManager component not found.");
        }

        AssetDatabase.SaveAssets();
        log.AppendLine("Done.");
        return log.ToString();
    }
}
