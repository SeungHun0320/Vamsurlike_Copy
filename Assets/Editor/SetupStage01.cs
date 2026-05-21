using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using Vamsurlike.Player;
using Vamsurlike.Core;

public class SetupStage01
{
    public static void Execute()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);

        // 기존 오브젝트 전부 제거 (중복 방지)
        foreach (var go in scene.GetRootGameObjects())
            Object.DestroyImmediate(go);

        // ── Directional Light ──────────────────────────────────────────
        var lightGo = new GameObject("Directional Light");
        var light   = lightGo.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.intensity = 1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── Ground ─────────────────────────────────────────────────────
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        ground.layer = LayerMask.NameToLayer("Ground");

        // ── Player ─────────────────────────────────────────────────────
        var player = new GameObject("Player");
        player.tag   = "Player";
        player.layer = LayerMask.NameToLayer("Player");
        player.transform.position = new Vector3(0f, 0.5f, 0f);

        var cc = player.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 0.5f, 0f);
        cc.height = 1.8f;
        cc.radius = 0.4f;

        player.AddComponent<PlayerInput>();
        player.AddComponent<PlayerStats>();
        player.AddComponent<PlayerController>();
        player.AddComponent<PlayerDamageReceiver>();
        player.AddComponent<PlayerAnimator>();

        // Capsule placeholder mesh
        var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cap.name = "Model";
        cap.transform.SetParent(player.transform, false);
        cap.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        Object.DestroyImmediate(cap.GetComponent<CapsuleCollider>());

        // ── GameInstance ───────────────────────────────────────────────
        var gi = new GameObject("GameInstance");
        gi.AddComponent<GameInstance>();
        gi.AddComponent<CoreFacade>();
        gi.AddComponent<GameManager>();

        // ── Main Camera + CinemachineBrain ─────────────────────────────
        var mainCamGo = new GameObject("Main Camera");
        mainCamGo.tag = "MainCamera";
        var mainCam = mainCamGo.AddComponent<Camera>();
        mainCam.fieldOfView = 40f;
        mainCamGo.AddComponent<AudioListener>();
        mainCamGo.AddComponent<CinemachineBrain>();
        // Initial position so editor view looks reasonable
        mainCamGo.transform.position = new Vector3(-9f, 15f, -9f);
        mainCamGo.transform.rotation = Quaternion.Euler(50f, 45f, 0f);

        // ── Cinemachine Virtual Camera ─────────────────────────────────
        // Offset direction inverse of camera forward at Euler(50, 45, 0)
        // forward ≈ (0.455, -0.766, 0.455)  →  back*14 ≈ (-6.4, 10.7, -6.4)
        var vcamGo = new GameObject("CM_FollowCam");
        vcamGo.transform.rotation = Quaternion.Euler(50f, 45f, 0f);

        var vcam = vcamGo.AddComponent<CinemachineCamera>();
        vcam.Follow = player.transform;
        vcam.LookAt = player.transform;

        var lens = vcam.Lens;
        lens.FieldOfView = 40f;
        vcam.Lens = lens;

        // Position: fixed-offset follow
        var follow = vcamGo.AddComponent<CinemachineFollow>();
        follow.FollowOffset = new Vector3(-18f, 30f, -18f);

        // Aim: hard look at player
        vcamGo.AddComponent<CinemachineHardLookAt>();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupStage01] Stage_01 setup complete.");
    }
}
