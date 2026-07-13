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
        [SerializeField] private string cameraTag = "PlayerFollowCamera";
        [SerializeField] private int activePriority = 20;

        private bool isBound;
        private float nextRetryTime;

        // 카메라 쉐이크 거리 감쇠 계산용 — 쿼터뷰 카메라 자신의 위치는 FollowOffset 때문에
        // 플레이어로부터 항상 멀리(수십 유닛) 떨어져 있어, 카메라 위치 기준으로 거리 감쇠를 계산하면
        // shakeRadius(10~15 정도)를 항상 초과해 강도가 0으로 죽어버린다(CameraShakeListener 참고).
        // 대신 로컬 플레이어 위치를 기준으로 계산하도록 여기서 참조를 공유한다.
        public static Transform LocalPlayerTransform { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LocalPlayerTransform = transform;
            BindCamera(logIfMissing: false);
        }

        public override void OnNetworkDespawn()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (LocalPlayerTransform == transform)
                LocalPlayerTransform = null;
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
            GameObject cameraObject = GameObject.FindGameObjectWithTag(cameraTag);
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
