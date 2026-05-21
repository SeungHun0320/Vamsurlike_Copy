using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class FixAnimatorPlacement
{
    public static void Execute()
    {
        var playerGo = GameObject.Find("Player");
        if (playerGo == null) { Debug.LogError("[FixAnimatorPlacement] Player not found."); return; }

        var meshObject = playerGo.transform.Find("Mesh Object");
        if (meshObject == null) { Debug.LogError("[FixAnimatorPlacement] Mesh Object not found."); return; }

        // 기존 Animator 전부 제거
        foreach (var anim in playerGo.GetComponentsInChildren<Animator>(true))
            Object.DestroyImmediate(anim);

        // Mesh Object에 Animator 추가
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/AC_Player.controller");
        var animator = meshObject.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[FixAnimatorPlacement] Animator placed on Mesh Object. Controller={controller?.name}");
    }
}
