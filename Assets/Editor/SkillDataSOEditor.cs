using UnityEditor;
using UnityEngine;
using Vamsurlike.Data;

[CustomEditor(typeof(SkillDataSO))]
[CanEditMultipleObjects]
public class SkillDataSOEditor : Editor
{
    private SerializedProperty skillName;
    private SerializedProperty icon;
    private SerializedProperty castType;
    private SerializedProperty isManual;
    private SerializedProperty projectilePrefab;
    private SerializedProperty maxLevel;
    private SerializedProperty levels;

    private void OnEnable()
    {
        skillName = serializedObject.FindProperty("skillName");
        icon = serializedObject.FindProperty("icon");
        castType = serializedObject.FindProperty("castType");
        isManual = serializedObject.FindProperty("isManual");
        projectilePrefab = serializedObject.FindProperty("projectilePrefab");
        maxLevel = serializedObject.FindProperty("maxLevel");
        levels = serializedObject.FindProperty("levels");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(skillName);
        EditorGUILayout.PropertyField(icon);
        EditorGUILayout.PropertyField(castType);
        EditorGUILayout.PropertyField(isManual);

        if (castType.hasMultipleDifferentValues)
        {
            EditorGUILayout.HelpBox("Select SkillData assets with the same Cast Type to edit type-specific fields.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        SkillCastType selectedCastType = (SkillCastType)castType.enumValueIndex;
        if (selectedCastType == SkillCastType.Projectile)
            EditorGUILayout.PropertyField(projectilePrefab);

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(maxLevel);
        DrawLevels(selectedCastType);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLevels(SkillCastType selectedCastType)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Levels", EditorStyles.boldLabel);

        int newSize = Mathf.Max(1, EditorGUILayout.IntField("Size", levels.arraySize));
        if (newSize != levels.arraySize)
            levels.arraySize = newSize;

        for (int i = 0; i < levels.arraySize; i++)
        {
            SerializedProperty level = levels.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            level.isExpanded = EditorGUILayout.Foldout(level.isExpanded, $"Level {i + 1}", true);
            if (level.isExpanded)
                DrawLevel(level, selectedCastType);
            EditorGUILayout.EndVertical();
        }
    }

    private static void DrawLevel(SerializedProperty level, SkillCastType selectedCastType)
    {
        DrawCommonFields(level);

        switch (selectedCastType)
        {
            case SkillCastType.Projectile:
                DrawProjectileFields(level);
                break;
            case SkillCastType.AreaAura:
                DrawPersistentFields(level);
                DrawAreaFields(level);
                break;
            case SkillCastType.Orbital:
                DrawPersistentFields(level);
                DrawOrbitalFields(level);
                break;
            case SkillCastType.Ultimate:
                DrawUltimateFields(level);
                break;
        }
    }

    private static void DrawCommonFields(SerializedProperty level)
    {
        EditorGUILayout.LabelField("Common", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(level.FindPropertyRelative("cooldown"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("damage"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("range"));
    }

    private static void DrawProjectileFields(SerializedProperty level)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Projectile", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(level.FindPropertyRelative("projectileSpeed"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("projectileLifetime"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("projectileHitRadius"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("projectileCount"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("spreadAngle"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("pierceCount"));
    }

    private static void DrawPersistentFields(SerializedProperty level)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Persistent", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(level.FindPropertyRelative("tickInterval"));
        var durationProp = level.FindPropertyRelative("duration");
        EditorGUILayout.PropertyField(durationProp);
        if (durationProp.floatValue > 0f)
            EditorGUILayout.HelpBox($"Active {durationProp.floatValue}s -> Cooldown (Common.cooldown)s cycle", MessageType.None);
        else
            EditorGUILayout.HelpBox("duration=0 : Always active (cooldown unused)", MessageType.None);
    }

    private static void DrawAreaFields(SerializedProperty level)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Area", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(level.FindPropertyRelative("areaRadius"));
    }

    private static void DrawOrbitalFields(SerializedProperty level)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Orbital", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(level.FindPropertyRelative("orbitalCount"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("orbitalRadius"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("orbitalRotationSpeed"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("orbitalHitRadius"));
    }

    private static void DrawUltimateFields(SerializedProperty level)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ultimate", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(level.FindPropertyRelative("projectileCount"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("projectileSpeed"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("projectileLifetime"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("projectileHitRadius"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("waveCount"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("waveDelay"));
        EditorGUILayout.PropertyField(level.FindPropertyRelative("rotationPerWave"));
    }
}
