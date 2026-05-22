using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

#if !UNITY_SERVER
using Unity.Cinemachine;
#endif

namespace Vamsurlike.Player
{
    public class LocalPlayerCameraBinder : NetworkBehaviour
    {
        [SerializeField] private string cameraName = "CM_FollowCam";
        [SerializeField] private int activePriority = 20;

        private bool isBound;
        private float nextRetryTime;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            BindCamera(logIfMissing: false);
        }

        public override void OnNetworkDespawn()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Update()
        {
            if (!IsOwner || isBound || Time.unscaledTime < nextRetryTime) return;
            nextRetryTime = Time.unscaledTime + 0.5f;
            BindCamera(logIfMissing: false);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsOwner) return;
            isBound = false;
            BindCamera(logIfMissing: false);
        }

        private void BindCamera(bool logIfMissing)
        {
#if UNITY_SERVER
            return;
#else
            CinemachineCamera camera = null;
            GameObject cameraObject = GameObject.Find(cameraName);
            if (cameraObject != null)
                camera = cameraObject.GetComponent<CinemachineCamera>();

            if (camera == null)
                camera = Object.FindFirstObjectByType<CinemachineCamera>();

            if (camera == null)
            {
                if (logIfMissing)
                    Debug.LogWarning($"[{nameof(LocalPlayerCameraBinder)}] 로컬 플레이어 카메라를 찾을 수 없습니다.");
                return;
            }

            camera.Follow = transform;
            camera.LookAt = transform;
            camera.Priority.Value = activePriority;
            isBound = true;
#endif
        }
    }
}
