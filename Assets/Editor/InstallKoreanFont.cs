using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;

public class InstallKoreanFont
{
    private const string FontDestDir  = "Assets/Resources/Fonts";
    private const string FontTtfPath  = "Assets/Resources/Fonts/MalgunGothic.ttf";
    private const string FontAssetPath = "Assets/Resources/Fonts/MalgunGothic SDF.asset";

    public static void Execute()
    {
        // 1. TMP Essentials 임포트 여부 확인
        if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
        {
            Debug.LogError("[KoreanFont] TMP Essentials가 아직 임포트되지 않았습니다. TMP Importer 창에서 먼저 임포트하세요.");
            return;
        }

        // 2. 맑은 고딕 TTF 복사
        CopyFontFromSystem();

        // 3. 복사된 폰트로 TMP Font Asset 생성 (Dynamic — 한글 온디맨드 생성)
        var fontAsset = CreateOrLoadFontAsset();
        if (fontAsset == null) return;

        // 4. TMP 기본 폰트로 지정
        SetTMPDefaultFont(fontAsset);

        // 5. 모든 씬의 TMP 텍스트에 폰트 적용
        ApplyFontToScenes(fontAsset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[KoreanFont] 맑은 고딕 TMP 폰트 설치 완료.");
    }

    private static void CopyFontFromSystem()
    {
        if (File.Exists(FontTtfPath)) return;

        // Windows 폰트 후보 순서대로 시도
        string[] candidates = {
            @"C:\Windows\Fonts\malgun.ttf",
            @"C:\Windows\Fonts\malgunbd.ttf",
            @"C:\Windows\Fonts\gulim.ttc",
        };

        foreach (var src in candidates)
        {
            if (!File.Exists(src)) continue;

            if (!Directory.Exists(FontDestDir))
                Directory.CreateDirectory(FontDestDir);

            File.Copy(src, FontTtfPath);
            AssetDatabase.ImportAsset(FontTtfPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[KoreanFont] 폰트 복사 완료: {src} → {FontTtfPath}");
            return;
        }

        Debug.LogError("[KoreanFont] Windows 시스템 폰트를 찾을 수 없습니다.");
    }

    private static TMP_FontAsset CreateOrLoadFontAsset()
    {
        // 기존 에셋 삭제 후 재생성 (sub-asset 누락 시 MissingReferenceException 방지)
        if (File.Exists(FontAssetPath))
            AssetDatabase.DeleteAsset(FontAssetPath);

        var font = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
        if (font == null)
        {
            Debug.LogError($"[KoreanFont] Font 로드 실패: {FontTtfPath}");
            return null;
        }

        // Dynamic 모드: 런타임에 필요한 글리프만 생성 — 한글 전체 사전 베이크 불필요
        var asset = TMP_FontAsset.CreateFontAsset(
            font,
            samplingPointSize: 90,
            atlasPadding: 9,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 2048,
            atlasHeight: 2048,
            atlasPopulationMode: AtlasPopulationMode.Dynamic
        );

        AssetDatabase.CreateAsset(asset, FontAssetPath);

        // atlas 텍스처와 머티리얼을 sub-asset으로 임베드 (누락 시 m_AtlasTextures 에러 발생)
        if (asset.atlasTextures != null)
            foreach (var tex in asset.atlasTextures)
                if (tex != null)
                    AssetDatabase.AddObjectToAsset(tex, FontAssetPath);

        if (asset.material != null)
            AssetDatabase.AddObjectToAsset(asset.material, FontAssetPath);

        AssetDatabase.SaveAssets();
        Debug.Log($"[KoreanFont] TMP FontAsset 생성: {FontAssetPath}");
        return asset;
    }

    private static void SetTMPDefaultFont(TMP_FontAsset fontAsset)
    {
        const string settingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath);
        if (settings == null)
        {
            Debug.LogWarning("[KoreanFont] TMP Settings 에셋을 찾을 수 없어 기본 폰트 지정을 건너뜁니다.");
            return;
        }

        var so = new SerializedObject(settings);
        var prop = so.FindProperty("m_defaultFontAsset");
        if (prop != null)
        {
            prop.objectReferenceValue = fontAsset;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            Debug.Log("[KoreanFont] TMP 기본 폰트 설정 완료.");
        }
    }

    private static void ApplyFontToScenes(TMP_FontAsset fontAsset)
    {
        string[] scenes = {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Bootstrap.unity",
            "Assets/Scenes/Stage_01.unity",
        };

        foreach (var scenePath in scenes)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool changed = false;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    // 직접 할당 시 에디터 모드에서 TMP가 텍스처 초기화를 시도해 에러 발생
                    // SerializedObject 경유로 참조만 교체
                    var so = new SerializedObject(tmp);
                    var prop = so.FindProperty("m_fontAsset");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = fontAsset;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(tmp);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                EditorSceneManager.SaveScene(scene, scenePath);
                Debug.Log($"[KoreanFont] {scenePath} TMP 폰트 적용 완료.");
            }
        }
    }
}
