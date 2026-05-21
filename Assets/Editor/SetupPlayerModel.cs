using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SetupPlayerModel
{
    public static void Execute()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);

        var playerGo = GameObject.Find("Player");
        if (playerGo == null) { Debug.LogError("[SetupPlayerModel] Player not found."); return; }

        // 기존 캡슐 플레이스홀더 제거
        var old = playerGo.transform.Find("Model");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // BE5 Player 프리팹 로드 & 자식으로 추가
        string prefabPath = "Assets/Resources/QuarterView 3D Action BE5/Prefabs/Player.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Debug.Log($"[SetupPlayerModel] Prefab load result: {(prefab == null ? "NULL" : prefab.name)} from {prefabPath}");
        if (prefab == null) { Debug.LogError("[SetupPlayerModel] Player prefab not found."); return; }

        var model = Object.Instantiate(prefab, playerGo.transform);
        model.name = "Model";
        Debug.Log($"[SetupPlayerModel] Instantiated: {model.name}, parent: {model.transform.parent?.name}, childCount: {playerGo.transform.childCount}");
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        // Animator Controller 할당
        var animator = model.GetComponentInChildren<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/AC_Player.controller");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupPlayerModel] Player model connected.");
    }
}
