using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;

public class FixCameraBinding
{
    public static void Execute()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage_01.unity", OpenSceneMode.Single);

        var follows = UnityEngine.Object.FindObjectsByType<CinemachineFollow>(
            UnityEngine.FindObjectsSortMode.None);

        foreach (var follow in follows)
        {
            var ts = follow.TrackerSettings;
            ts.BindingMode      = BindingMode.WorldSpace;
            ts.PositionDamping  = UnityEngine.Vector3.zero; // 뱀서류는 카메라가 즉시 따라와야 함
            ts.RotationDamping  = UnityEngine.Vector3.zero;
            follow.TrackerSettings = ts;
            UnityEngine.Debug.Log($"[FixCameraBinding] {follow.name} → WorldSpace, damping 0");
        }

        EditorSceneManager.SaveScene(scene);
    }
}
