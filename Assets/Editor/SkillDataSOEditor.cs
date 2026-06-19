using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Editor
{
    [CustomEditor(typeof(SkillDataSO))]
    public class SkillDataSOEditor : UnityEditor.Editor
    {
        private static readonly Dictionary<string, bool> Foldouts = new();

        private SerializedProperty skillName;
        private SerializedProperty icon;
        private SerializedProperty castType;
        private SerializedProperty isManual;
        private SerializedProperty projectilePrefab;
        private SerializedProperty vfxPrefab;
        private SerializedProperty maxLevel;
        private SerializedProperty levels;

        private void OnEnable()
        {
            skillName        = serializedObject.FindProperty(nameof(SkillDataSO.skillName));
            icon             = serializedObject.FindProperty(nameof(SkillDataSO.icon));
            castType         = serializedObject.FindProperty(nameof(SkillDataSO.castType));
            isManual         = serializedObject.FindProperty(nameof(SkillDataSO.isManual));
            projectilePrefab = serializedObject.FindProperty(nameof(SkillDataSO.projectilePrefab));
            vfxPrefab        = serializedObject.FindProperty(nameof(SkillDataSO.vfxPrefab));
            maxLevel         = serializedObject.FindProperty(nameof(SkillDataSO.maxLevel));
            levels           = serializedObject.FindProperty(nameof(SkillDataSO.levels));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(skillName);
            EditorGUILayout.PropertyField(icon);
            EditorGUILayout.PropertyField(castType);
            EditorGUILayout.PropertyField(isManual);

            SkillCastType type = (SkillCastType)castType.enumValueIndex;
            if (UsesProjectilePrefab(type))
                EditorGUILayout.PropertyField(projectilePrefab);
            if (UsesVFXPrefab(type))
                EditorGUILayout.PropertyField(vfxPrefab);

            EditorGUILayout.Space(6f);
            EditorGUILayout.PropertyField(maxLevel);
            DrawLevels(type);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLevels(SkillCastType type)
        {
            int size = Mathf.Max(0, EditorGUILayout.IntField("Levels Size", levels.arraySize));
            if (size != levels.arraySize)
                levels.arraySize = size;

            EditorGUI.indentLevel++;
            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                string key = level.propertyPath;
                if (!Foldouts.ContainsKey(key))
                    Foldouts[key] = true;

                Foldouts[key] = EditorGUILayout.Foldout(Foldouts[key], $"Level {i + 1}", true);
                if (!Foldouts[key]) continue;

                EditorGUI.indentLevel++;
                DrawLevelFields(level, type);
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawLevelFields(SerializedProperty level, SkillCastType type)
        {
            DrawSection("Common");
            DrawField(level, "cooldown");
            DrawField(level, "damage");

            if (UsesRange(type))
                DrawField(level, "range");

            switch (type)
            {
                case SkillCastType.Projectile:
                    DrawProjectileSection(level, includeCountAndSpread: true, includePierce: true);
                    break;

                case SkillCastType.AreaAura:
                    DrawPersistentSection(level);
                    DrawSection("Area");
                    DrawField(level, "areaRadius");
                    break;

                case SkillCastType.Orbital:
                    DrawPersistentSection(level);
                    DrawOrbitalSection(level, includeProjectileDetach: false);
                    break;

                case SkillCastType.OrbitalGrenade:
                    DrawProjectileSection(level, includeCountAndSpread: false, includePierce: true);
                    DrawOrbitalSection(level, includeProjectileDetach: true);
                    break;

                case SkillCastType.Ultimate:
                    DrawProjectileSection(level, includeCountAndSpread: true, includePierce: true);
                    DrawSection("Ultimate");
                    DrawField(level, "waveCount");
                    DrawField(level, "waveDelay");
                    DrawField(level, "rotationPerWave");
                    break;

                case SkillCastType.Grenade:
                    DrawSection("Grenade");
                    DrawField(level, "grenadeRange");
                    DrawField(level, "grenadeArcHeight");
                    DrawField(level, "splashRadius");
                    break;

                case SkillCastType.ScatterShot:
                    DrawProjectileSection(level, includeCountAndSpread: false, includePierce: true);
                    DrawSection("Scatter Shot");
                    DrawField(level, "scatterBulletCount");
                    DrawField(level, "scatterAngle");
                    DrawField(level, "burstDuration");
                    break;

                case SkillCastType.Melee:
                    DrawSection("Melee");
                    DrawField(level, "meleeArcAngle");
                    DrawField(level, "meleeRange");
                    break;
            }
        }

        private static void DrawProjectileSection(SerializedProperty level, bool includeCountAndSpread, bool includePierce)
        {
            DrawSection("Projectile");
            DrawField(level, "projectileSpeed");
            DrawField(level, "projectileLifetime");
            DrawField(level, "projectileHitRadius");

            if (includeCountAndSpread)
            {
                DrawField(level, "projectileCount");
                DrawField(level, "spreadAngle");
            }

            if (includePierce)
                DrawField(level, "pierceCount");
        }

        private static void DrawPersistentSection(SerializedProperty level)
        {
            DrawSection("Persistent");
            DrawField(level, "duration");
            DrawField(level, "tickInterval");
        }

        private static void DrawOrbitalSection(SerializedProperty level, bool includeProjectileDetach)
        {
            DrawSection("Orbital");
            DrawField(level, "orbitalCount");
            DrawField(level, "orbitalRadius");
            DrawField(level, "orbitalRotationSpeed");
            DrawField(level, "orbitalHitRadius");
            DrawField(level, "orbitalDamageMultiplier");
            DrawField(level, "orbitalHitCooldown");

            if (!includeProjectileDetach) return;

            DrawField(level, "orbitalProjectileScale");
            DrawField(level, "detachedOrbitalLifetimeMultiplier");
            DrawField(level, "detachedOrbitalHomingDelay");
            DrawField(level, "detachedOrbitalHomingRange");
            DrawField(level, "detachedOrbitalHomingTurnSpeed");
        }

        private static void DrawField(SerializedProperty owner, string propertyName)
        {
            SerializedProperty property = owner.FindPropertyRelative(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property);
        }

        private static void DrawSection(string label)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        private static bool UsesProjectilePrefab(SkillCastType type) =>
            type == SkillCastType.Projectile ||
            type == SkillCastType.ScatterShot ||
            type == SkillCastType.Ultimate ||
            type == SkillCastType.OrbitalGrenade;

        private static bool UsesVFXPrefab(SkillCastType type) =>
            type == SkillCastType.Grenade ||
            type == SkillCastType.Melee;

        private static bool UsesRange(SkillCastType type) =>
            type == SkillCastType.Projectile ||
            type == SkillCastType.AreaAura ||
            type == SkillCastType.ScatterShot ||
            type == SkillCastType.OrbitalGrenade;
    }
}
