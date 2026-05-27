using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vamsurlike.Data;
using Vamsurlike.Network;
using Vamsurlike.Skills;

public static class SetupPhase4ProjectileSkill
{
    private const string ProjectilePrefabPath = "Assets/Prefabs/Skills/BasicProjectile.prefab";
    private const string ProjectileMaterialPath = "Assets/Resources/Materials/BasicProjectile_Mat.mat";
    private const string BasicProjectileSkillDataPath = "Assets/Data/Skills/SkillData_BasicProjectile.asset";
    private const string SpreadProjectileSkillDataPath = "Assets/Data/Skills/SkillData_SpreadProjectile.asset";
    private const string PierceProjectileSkillDataPath = "Assets/Data/Skills/SkillData_PierceProjectile.asset";
    private const string DamageAuraSkillDataPath = "Assets/Data/Skills/SkillData_DamageAura.asset";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/NetworkedPlayer.prefab";
    private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
    private const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
    private static readonly Vector3 DefaultProjectileVisualRotationOffsetEuler = new(0f, -90f, 0f);

    [MenuItem("Tools/Vamsurlike/Setup Phase 4 Projectile Skill")]
    public static void Execute()
    {
        EnsureFolders();
        GameObject projectilePrefab = EnsureProjectilePrefab();
        SkillDataSO basicProjectile = EnsureBasicProjectileSkillData(projectilePrefab);
        EnsureSpreadProjectileSkillData(projectilePrefab);
        EnsurePierceProjectileSkillData(projectilePrefab);
        EnsureDamageAuraSkillData();
        EnsurePlayerSkillManager(basicProjectile);
        EnsureDefaultNetworkPrefab(projectilePrefab);
        EnsurePoolConfig(projectilePrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupPhase4ProjectileSkill] 기본 총알 스킬 설정 완료");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Prefabs", "Skills");
        EnsureFolder("Assets/Data", "Skills");
        EnsureFolder("Assets/Resources", "Materials");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static GameObject EnsureProjectilePrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
        if (existing != null)
        {
            ConfigureProjectilePrefab(existing);
            return existing;
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BasicProjectile";
        go.layer = LayerMask.NameToLayer("Projectile");
        go.transform.localScale = Vector3.one * 0.25f;

        Object.DestroyImmediate(go.GetComponent<SphereCollider>());
        go.AddComponent<NetworkObject>();
        go.AddComponent<NetworkTransform>();
        go.AddComponent<NetworkProjectile>();

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var mat = new Material(renderer.sharedMaterial)
            {
                color = new Color(0.2f, 0.85f, 1f)
            };
            AssetDatabase.CreateAsset(mat, ProjectileMaterialPath);
            renderer.sharedMaterial = mat;
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePrefabPath);
        Object.DestroyImmediate(go);
        ConfigureProjectilePrefab(prefab);
        return prefab;
    }

    private static void ConfigureProjectilePrefab(GameObject prefab)
    {
        if (prefab == null) return;

        var root = PrefabUtility.LoadPrefabContents(ProjectilePrefabPath);
        try
        {
            var projectile = root.GetComponent<NetworkProjectile>();
            if (projectile == null)
                projectile = root.AddComponent<NetworkProjectile>();

            var serialized = new SerializedObject(projectile);
            serialized.FindProperty("visualRotationOffsetEuler").vector3Value = DefaultProjectileVisualRotationOffsetEuler;
            serialized.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static SkillDataSO EnsureBasicProjectileSkillData(GameObject projectilePrefab)
    {
        return EnsureSkillData(
            BasicProjectileSkillDataPath,
            "Basic Projectile",
            SkillCastType.Projectile,
            projectilePrefab,
            new[]
            {
                new SkillLevelData
                {
                    cooldown = 0.75f,
                    damage = 15f,
                    range = 12f,
                    projectileSpeed = 14f,
                    projectileLifetime = 2.5f,
                    projectileHitRadius = 0.7f,
                    projectileCount = 1,
                    spreadAngle = 0f,
                    pierceCount = 0,
                    areaRadius = 0f,
                    tickInterval = 1f
                }
            });
    }

    private static SkillDataSO EnsureSpreadProjectileSkillData(GameObject projectilePrefab)
    {
        return EnsureSkillData(
            SpreadProjectileSkillDataPath,
            "Spread Projectile",
            SkillCastType.Projectile,
            projectilePrefab,
            new[]
            {
                new SkillLevelData
                {
                    cooldown = 1.2f,
                    damage = 10f,
                    range = 12f,
                    projectileSpeed = 13f,
                    projectileLifetime = 2.5f,
                    projectileHitRadius = 0.65f,
                    projectileCount = 3,
                    spreadAngle = 30f,
                    pierceCount = 0,
                    areaRadius = 0f,
                    tickInterval = 1f
                },
                new SkillLevelData
                {
                    cooldown = 1.1f,
                    damage = 12f,
                    range = 13f,
                    projectileSpeed = 14f,
                    projectileLifetime = 2.7f,
                    projectileHitRadius = 0.7f,
                    projectileCount = 5,
                    spreadAngle = 45f,
                    pierceCount = 0,
                    areaRadius = 0f,
                    tickInterval = 1f
                }
            });
    }

    private static SkillDataSO EnsurePierceProjectileSkillData(GameObject projectilePrefab)
    {
        return EnsureSkillData(
            PierceProjectileSkillDataPath,
            "Pierce Projectile",
            SkillCastType.Projectile,
            projectilePrefab,
            new[]
            {
                new SkillLevelData
                {
                    cooldown = 1.0f,
                    damage = 18f,
                    range = 14f,
                    projectileSpeed = 15f,
                    projectileLifetime = 2.8f,
                    projectileHitRadius = 0.7f,
                    projectileCount = 1,
                    spreadAngle = 0f,
                    pierceCount = 2,
                    areaRadius = 0f,
                    tickInterval = 1f
                },
                new SkillLevelData
                {
                    cooldown = 0.95f,
                    damage = 22f,
                    range = 15f,
                    projectileSpeed = 16f,
                    projectileLifetime = 3f,
                    projectileHitRadius = 0.75f,
                    projectileCount = 1,
                    spreadAngle = 0f,
                    pierceCount = 4,
                    areaRadius = 0f,
                    tickInterval = 1f
                }
            });
    }

    private static SkillDataSO EnsureDamageAuraSkillData()
    {
        return EnsureSkillData(
            DamageAuraSkillDataPath,
            "Damage Aura",
            SkillCastType.AreaAura,
            null,
            new[]
            {
                new SkillLevelData
                {
                    cooldown = 0.8f,
                    damage = 6f,
                    range = 3f,
                    projectileSpeed = 1f,
                    projectileLifetime = 1f,
                    projectileHitRadius = 0.5f,
                    projectileCount = 1,
                    spreadAngle = 0f,
                    pierceCount = 0,
                    areaRadius = 3f,
                    tickInterval = 0.8f
                },
                new SkillLevelData
                {
                    cooldown = 0.65f,
                    damage = 9f,
                    range = 3.5f,
                    projectileSpeed = 1f,
                    projectileLifetime = 1f,
                    projectileHitRadius = 0.5f,
                    projectileCount = 1,
                    spreadAngle = 0f,
                    pierceCount = 0,
                    areaRadius = 3.5f,
                    tickInterval = 0.65f
                }
            });
    }

    private static SkillDataSO EnsureSkillData(
        string path,
        string skillName,
        SkillCastType castType,
        GameObject projectilePrefab,
        SkillLevelData[] levels)
    {
        var skillData = AssetDatabase.LoadAssetAtPath<SkillDataSO>(path);
        if (skillData == null)
        {
            skillData = ScriptableObject.CreateInstance<SkillDataSO>();
            AssetDatabase.CreateAsset(skillData, path);
        }

        skillData.skillName = skillName;
        skillData.castType = castType;
        skillData.isManual = false;
        skillData.projectilePrefab = projectilePrefab;
        skillData.maxLevel = Mathf.Max(1, levels != null ? levels.Length : 1);
        skillData.levels = levels;

        EditorUtility.SetDirty(skillData);
        return skillData;
    }

    private static void EnsurePlayerSkillManager(SkillDataSO skillData)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var manager = root.GetComponent<SkillManager>();
            if (manager == null)
                manager = root.AddComponent<SkillManager>();

            var serialized = new SerializedObject(manager);
            var skills = serialized.FindProperty("startingSkills");
            skills.arraySize = 1;
            skills.GetArrayElementAtIndex(0).objectReferenceValue = skillData;
            serialized.FindProperty("failedCastRetryDelay").floatValue = 0.1f;
            serialized.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureDefaultNetworkPrefab(GameObject projectilePrefab)
    {
        var networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
        if (networkPrefabs == null)
        {
            Debug.LogWarning($"[SetupPhase4ProjectileSkill] NetworkPrefabsList를 찾을 수 없습니다: {NetworkPrefabsPath}");
            return;
        }

        var serialized = new SerializedObject(networkPrefabs);
        var list = serialized.FindProperty("List");
        if (!ContainsPrefab(list, projectilePrefab))
        {
            int index = list.arraySize;
            list.arraySize++;
            var element = list.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("Override").boolValue = false;
            element.FindPropertyRelative("Prefab").objectReferenceValue = projectilePrefab;
            element.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null;
            element.FindPropertyRelative("SourceHashToOverride").ulongValue = 0;
            element.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;
        }
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(networkPrefabs);
    }

    private static bool ContainsPrefab(SerializedProperty list, GameObject prefab)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            var element = list.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative("Prefab").objectReferenceValue == prefab)
                return true;
        }
        return false;
    }

    private static void EnsurePoolConfig(GameObject projectilePrefab)
    {
        var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
        {
            var pool = root.GetComponent<PoolManager>();
            if (pool == null) continue;

            var serialized = new SerializedObject(pool);
            var configs = serialized.FindProperty("networkConfigs");
            if (!ContainsPoolConfig(configs, projectilePrefab))
            {
                int index = configs.arraySize;
                configs.arraySize++;
                var element = configs.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("prefab").objectReferenceValue = projectilePrefab;
                element.FindPropertyRelative("warmupCount").intValue = 50;
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(pool);
            break;
        }
        EditorSceneManager.SaveScene(scene);
    }

    private static bool ContainsPoolConfig(SerializedProperty configs, GameObject prefab)
    {
        for (int i = 0; i < configs.arraySize; i++)
        {
            var element = configs.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative("prefab").objectReferenceValue == prefab)
                return true;
        }
        return false;
    }
}
