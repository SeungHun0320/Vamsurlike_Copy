using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vamsurlike.Data;
using Vamsurlike.Stage;
using Vamsurlike.Upgrades;
using Vamsurlike.UI;

public class Phase5Setup
{
    public static void Execute()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Data", "Upgrades");

        var options = CreateUpgradeOptions();

        var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>("Assets/Resources/UpgradeCatalog.asset");
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<UpgradeCatalog>();
            AssetDatabase.CreateAsset(catalog, "Assets/Resources/UpgradeCatalog.asset");
        }
        catalog.options = options;
        EditorUtility.SetDirty(catalog);

        CreateLevelSystem();
        AddPassiveStatHandlerToPrefab();
        CreateLevelUpUI();

        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Phase5Setup] ✅ 완료: UpgradeCatalog {options.Length}종 등록");
    }

    // ────────────────────────────────────────────────────────────────
    static UpgradeOptionSO[] CreateUpgradeOptions()
    {
        // 패시브 스탯 옵션
        var passiveDefs = new[]
        {
            ("MaxHP_20",       "HP +20",        "최대 체력이 20 증가합니다.",          UpgradeEffectType.PassiveMaxHP,        20f),
            ("MaxHP_40",       "HP +40",        "최대 체력이 40 증가합니다.",          UpgradeEffectType.PassiveMaxHP,        40f),
            ("MoveSpeed_05",   "이동속도 +0.5", "이동 속도가 0.5 증가합니다.",         UpgradeEffectType.PassiveMoveSpeed,    0.5f),
            ("MoveSpeed_10",   "이동속도 +1.0", "이동 속도가 1.0 증가합니다.",         UpgradeEffectType.PassiveMoveSpeed,    1.0f),
            ("Attack_010",     "공격력 +10%",   "스킬 공격력 배율이 10% 증가합니다.", UpgradeEffectType.PassiveAttackPower,  0.1f),
            ("PickupRadius_1", "흡수 범위 +1",  "XP 오브 흡수 반경이 1 증가합니다.",  UpgradeEffectType.PassivePickupRadius, 1.0f),
        };

        // 스킬 레벨업 옵션 (보유 스킬 레벨업 / 미보유 시 습득)
        var skillDefs = new[]
        {
            ("Skill_BasicProj",   "기본 투사체 강화",    "기본 투사체 스킬을 레벨업합니다.",   "Assets/Data/Skills/SD_BasicProjectile.asset"),
            ("Skill_PierceProj",  "관통 투사체 강화",    "관통 투사체 스킬을 레벨업합니다.",   "Assets/Data/Skills/SD_PierceProjectile.asset"),
            ("Skill_SpreadProj",  "산탄 투사체 강화",    "산탄 투사체 스킬을 레벨업합니다.",   "Assets/Data/Skills/SD_SpreadProjectile.asset"),
            ("Skill_BulletStorm", "총알 폭풍 강화",      "총알 폭풍 스킬을 레벨업합니다.",     "Assets/Data/Skills/SD_BulletStorm.asset"),
            ("Skill_DamageAura",  "데미지 오라 강화",    "데미지 오라 스킬을 레벨업합니다.",   "Assets/Data/Skills/SD_DamageAura.asset"),
            ("Skill_Orbital",     "궤도체 강화",         "궤도체 스킬을 레벨업합니다.",        "Assets/Data/Skills/SD_Orbital.asset"),
        };

        var list = new System.Collections.Generic.List<UpgradeOptionSO>();

        // 패시브 옵션 생성
        foreach (var (id, name, desc, type, val) in passiveDefs)
        {
            string path = $"Assets/Data/Upgrades/UpgradeOption_{id}.asset";
            var so = LoadOrCreate<UpgradeOptionSO>(path);
            so.upgradeName = name;
            so.description = desc;
            so.effectType  = type;
            so.value       = val;
            so.skillData   = null;
            EditorUtility.SetDirty(so);
            list.Add(so);
        }

        // 스킬 레벨업 옵션 생성
        foreach (var (id, name, desc, skillPath) in skillDefs)
        {
            string path = $"Assets/Data/Upgrades/UpgradeOption_{id}.asset";
            var so = LoadOrCreate<UpgradeOptionSO>(path);
            so.upgradeName = name;
            so.description = desc;
            so.effectType  = UpgradeEffectType.SkillLevelUp;
            so.value       = 0f;
            so.skillData   = AssetDatabase.LoadAssetAtPath<SkillDataSO>(skillPath);

            if (so.skillData == null)
                Debug.LogWarning($"[Phase5Setup] 스킬 에셋 없음: {skillPath}");

            EditorUtility.SetDirty(so);
            list.Add(so);
        }

        return list.ToArray();
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        var so = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(so, path);
        return so;
    }

    // ────────────────────────────────────────────────────────────────
    static void CreateLevelSystem()
    {
        if (GameObject.Find("LevelSystem") != null) return;

        var go = new GameObject("LevelSystem");
        go.AddComponent<NetworkObject>();
        go.AddComponent<LevelUpManager>();
        go.AddComponent<SharedLevelSystem>();

        var parent = GameObject.Find("Core");
        if (parent != null) go.transform.SetParent(parent.transform);

        Debug.Log("[Phase5Setup] LevelSystem 생성 완료");
    }

    // ────────────────────────────────────────────────────────────────
    static void AddPassiveStatHandlerToPrefab()
    {
        const string prefabPath = "Assets/Prefabs/Player/NetworkedPlayer.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogWarning($"[Phase5Setup] 프리팹 없음: {prefabPath}"); return; }
        if (prefab.GetComponent<PassiveStatHandler>() != null) return;

        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        contents.AddComponent<PassiveStatHandler>();
        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
        Debug.Log("[Phase5Setup] NetworkedPlayer에 PassiveStatHandler 추가 완료");
    }

    // ────────────────────────────────────────────────────────────────
    static void CreateLevelUpUI()
    {
        if (GameObject.Find("LevelUpCanvas") != null) return;

        var canvasGO = new GameObject("LevelUpCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var panelGO   = new GameObject("LevelUpPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImg  = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.7f);
        var panelRect  = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        var titleGO  = new GameObject("TitleText");
        titleGO.transform.SetParent(panelGO.transform, false);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "레벨 업!";
        titleTMP.fontSize  = 48;
        titleTMP.alignment = TextAlignmentOptions.Center;
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.75f);
        titleRect.anchorMax = new Vector2(1f, 0.95f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;

        float[] xAnchors = { 0.05f, 0.37f, 0.69f };
        var cardUIs = new LevelUpCardUI[3];
        for (int i = 0; i < 3; i++)
        {
            var cardGO  = new GameObject($"Card_{i + 1}");
            cardGO.transform.SetParent(panelGO.transform, false);
            var cardImg = cardGO.AddComponent<Image>();
            cardImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            var cardRect  = cardGO.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(xAnchors[i], 0.2f);
            cardRect.anchorMax = new Vector2(xAnchors[i] + 0.26f, 0.72f);
            cardRect.offsetMin = cardRect.offsetMax = Vector2.zero;

            var nameGO  = new GameObject("NameText");
            nameGO.transform.SetParent(cardGO.transform, false);
            var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text      = "업그레이드";
            nameTMP.fontSize  = 22;
            nameTMP.alignment = TextAlignmentOptions.Center;
            var nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.7f);
            nameRect.anchorMax = new Vector2(1f, 0.95f);
            nameRect.offsetMin = new Vector2(8f, 0f);
            nameRect.offsetMax = new Vector2(-8f, 0f);

            var descGO  = new GameObject("DescText");
            descGO.transform.SetParent(cardGO.transform, false);
            var descTMP = descGO.AddComponent<TextMeshProUGUI>();
            descTMP.text      = "설명";
            descTMP.fontSize  = 16;
            descTMP.alignment = TextAlignmentOptions.Center;
            var descRect = descGO.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 0.25f);
            descRect.anchorMax = new Vector2(1f, 0.68f);
            descRect.offsetMin = new Vector2(8f, 0f);
            descRect.offsetMax = new Vector2(-8f, 0f);

            var btnGO  = new GameObject("SelectButton");
            btnGO.transform.SetParent(cardGO.transform, false);
            var btn    = btnGO.AddComponent<Button>();
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.5f, 0.9f, 1f);
            var btnRect  = btnGO.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.1f, 0.05f);
            btnRect.anchorMax = new Vector2(0.9f, 0.22f);
            btnRect.offsetMin = btnRect.offsetMax = Vector2.zero;

            var lblGO  = new GameObject("Label");
            lblGO.transform.SetParent(btnGO.transform, false);
            var lblTMP = lblGO.AddComponent<TextMeshProUGUI>();
            lblTMP.text      = "선택";
            lblTMP.fontSize  = 18;
            lblTMP.alignment = TextAlignmentOptions.Center;
            var lblRect = lblGO.GetComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = lblRect.offsetMax = Vector2.zero;

            var cardUI = cardGO.AddComponent<LevelUpCardUI>();
            var so     = new SerializedObject(cardUI);
            so.FindProperty("nameText").objectReferenceValue    = nameTMP;
            so.FindProperty("descText").objectReferenceValue    = descTMP;
            so.FindProperty("selectButton").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();
            cardUIs[i] = cardUI;
        }

        // LevelUpUI는 Canvas에 붙인다 — Panel이 inactive여도 이벤트 구독이 유지돼야 함
        var uiComp = canvasGO.AddComponent<LevelUpUI>();
        var uiSO   = new SerializedObject(uiComp);
        uiSO.FindProperty("panel").objectReferenceValue = panelGO;
        var cardsProp = uiSO.FindProperty("cards");
        cardsProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
            cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cardUIs[i];
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        // Panel만 비활성. Canvas는 항상 활성 유지 → OnEnable 정상 구독
        panelGO.SetActive(false);
        Debug.Log("[Phase5Setup] LevelUpUI Canvas 생성 완료");
    }

    static void EnsureFolder(string parent, string child)
    {
        string full = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(full))
            AssetDatabase.CreateFolder(parent, child);
    }
}
