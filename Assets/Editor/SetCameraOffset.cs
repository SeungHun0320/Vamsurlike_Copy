using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;

public class SetCameraOffset
{
    public static void Execute()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);

        var vcamGo = GameObject.Find("CM_FollowCam");
        if (vcamGo == null) { Debug.LogError("[SetCameraOffset] CM_FollowCam not found."); return; }

        var follow = vcamGo.GetComponent<CinemachineFollow>();
        if (follow == null) { Debug.LogError("[SetCameraOffset] CinemachineFollow not found."); return; }

        follow.FollowOffset = new Vector3(-18f, 30f, -18f);

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SetCameraOffset] FollowOffset set to {follow.FollowOffset}");
    }
}
