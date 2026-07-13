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

            // levels 배열 크기는 항상 maxLevel과 동기화한다. 예전엔 별도의 수동 "Levels Size" IntField로
            // levels.arraySize를 직접 편집할 수 있었는데, 어셈블리 리로드 직후 등 GUI 값이 일시적으로
            // 어긋나는 상황에서 실수로 더 작은 값이 커밋되면 기존 레벨 데이터가 확인 없이 조용히
            // 삭제되는 사고가 있었다(대지분쇄자 3레벨 → 1레벨로 유실). maxLevel 하나만 신뢰 가능한
            // 소스로 두고 배열 크기를 거기서만 파생시킨다.
            int targetSize = Mathf.Max(1, maxLevel.intValue);
            if (levels.arraySize != targetSize)
                levels.arraySize = targetSize;

            DrawLevels(type);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLevels(SkillCastType type)
        {
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
                    DrawSection("Lifesteal");
                    DrawField(level, "lifestealRatio");
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
                    DrawPersistentSection(level);
                    DrawOrbitalSection(level, includeProjectileDetach: false);
                    DrawSection("Orbital Grenade (충격 궤도)");
                    DrawField(level, "orbitalKnockbackForce");
                    break;

                case SkillCastType.BlackHole:
                    DrawPersistentSection(level);
                    DrawSection("Area");
                    DrawField(level, "areaRadius");
                    DrawSection("BlackHole");
                    DrawField(level, "pullSpeed");
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

                case SkillCastType.ClusterGrenade:
                    DrawSection("Grenade");
                    DrawField(level, "grenadeRange");
                    DrawField(level, "grenadeArcHeight");
                    DrawField(level, "splashRadius");
                    DrawSection("Cluster Grenade");
                    DrawField(level, "clusterCount");
                    DrawField(level, "clusterSpread");
                    DrawField(level, "clusterSplashRadius");
                    DrawField(level, "clusterDamageRatio");
                    break;

                case SkillCastType.ScatterShot:
                    DrawProjectileSection(level, includeCountAndSpread: false, includePierce: true);
                    DrawSection("Scatter Shot (기관총)");
                    DrawField(level, "scatterBulletCount");
                    DrawField(level, "scatterAngle");
                    DrawField(level, "burstDuration");
                    break;

                case SkillCastType.Melee:
                    DrawSection("Melee (사각형 판정)");
                    DrawField(level, "meleeRange");
                    DrawField(level, "meleeWidth");
                    break;

                case SkillCastType.PierceShotgun:
                case SkillCastType.Shotgun:
                    DrawSection("Shotgun (원뿔 즉발)");
                    DrawField(level, "scatterAngle");
                    DrawField(level, "shotgunSoloDamageMultiplier");
                    DrawField(level, "shotgunSharedDamageMultiplier");
                    break;

                case SkillCastType.Earthshatter:
                    DrawSection("Melee (사각형 판정)");
                    DrawField(level, "meleeRange");
                    DrawField(level, "meleeWidth");
                    DrawSection("Earthshatter (대지분쇄자)");
                    DrawField(level, "stunDuration");
                    DrawField(level, "aftershockCount");
                    DrawField(level, "aftershockRadius");
                    DrawField(level, "aftershockDamageRatio");
                    DrawField(level, "aftershockDelay");
                    break;

                case SkillCastType.GaleSpread:
                    DrawProjectileSection(level, includeCountAndSpread: false, includePierce: false);
                    DrawSection("Scatter Shot (기관총 재사용)");
                    DrawField(level, "scatterBulletCount");
                    DrawField(level, "scatterAngle");
                    DrawField(level, "burstDuration");
                    break;

                case SkillCastType.PiercingBoomerang:
                    DrawSection("Projectile");
                    DrawField(level, "projectileSpeed");
                    DrawField(level, "projectileHitRadius");
                    DrawSection("Piercing Boomerang (왕복 관통창)");
                    DrawField(level, "boomerangDamageAmplifyPerStack");
                    break;

                case SkillCastType.LifeDrainBolt:
                    DrawOrbitalSection(level, includeProjectileDetach: true);
                    DrawSection("Lifesteal");
                    DrawField(level, "lifestealRatio");
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
            type == SkillCastType.GaleSpread ||
            type == SkillCastType.LifeDrainBolt;

        private static bool UsesVFXPrefab(SkillCastType type) =>
            type == SkillCastType.Grenade ||
            type == SkillCastType.ClusterGrenade ||
            type == SkillCastType.Melee ||
            type == SkillCastType.Earthshatter;

        private static bool UsesRange(SkillCastType type) =>
            type == SkillCastType.Projectile ||
            type == SkillCastType.AreaAura ||
            type == SkillCastType.ScatterShot ||
            type == SkillCastType.BlackHole ||
            type == SkillCastType.PierceShotgun ||
            type == SkillCastType.Shotgun ||
            type == SkillCastType.PiercingBoomerang ||
            type == SkillCastType.LifeDrainBolt;
    }
}
