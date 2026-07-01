using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.UI
{
    // Bootstrap 씬 Canvas에 배치. DontDestroyOnLoad로 씬 전환 전 구간도 커버.
    // NetworkManager.SceneManager.OnSceneEvent를 지연 구독(Update)해
    // 네트워크 시작 전/후 모두 안전하게 동작하며, 재접속 시에도 자동 재구독된다.
    //
    // 표시 조건: SceneEventType.Load (서버가 씬 전환 시작)
    // 숨김 조건: SceneEventType.SynchronizeComplete (클라이언트 — 스폰 포함 동기화 완료)
    //            SceneEventType.LoadEventCompleted   (서버 — 모든 클라이언트 로드 완료)
    public class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance { get; private set; }

        [SerializeField] private GameObject panel;

        private NetworkManager subscribedManager;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        private void Update()
        {
            TrySubscribe();
        }

        public void Show() { if (panel != null) panel.SetActive(true); }
        public void Hide() { if (panel != null) panel.SetActive(false); }

        private void TrySubscribe()
        {
            var nm = NetworkManager.Singleton;

            // 네트워크가 종료됐으면 구독 해제
            if (nm == null || !nm.IsListening || nm.SceneManager == null)
            {
                if (subscribedManager != null) Unsubscribe();
                return;
            }

            if (subscribedManager == nm) return;

            // 이전 인스턴스 정리 후 재구독 (재접속 시 대응)
            Unsubscribe();
            nm.SceneManager.OnSceneEvent += OnSceneEvent;
            nm.OnServerStopped           += OnNetworkStopped;
            nm.OnClientStopped           += OnNetworkStopped;
            subscribedManager = nm;
        }

        private void Unsubscribe()
        {
            if (subscribedManager == null) return;

            if (subscribedManager.SceneManager != null)
                subscribedManager.SceneManager.OnSceneEvent -= OnSceneEvent;

            subscribedManager.OnServerStopped -= OnNetworkStopped;
            subscribedManager.OnClientStopped -= OnNetworkStopped;
            subscribedManager = null;
        }

        private void OnNetworkStopped(bool _)
        {
            Unsubscribe();
            Hide();
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.Load:
                    Show();
                    break;

                // 클라이언트: 씬 로드 후 NGO NetworkObject 동기화(스폰)까지 완료
                case SceneEventType.SynchronizeComplete:
                // 서버(또는 전체): 모든 클라이언트 로드 완료 알림
                case SceneEventType.LoadEventCompleted:
                    Hide();
                    break;
            }
        }
    }
}
