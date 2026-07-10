using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vamsurlike.Core;
using Vamsurlike.Network;

namespace Vamsurlike.UI
{
    // Bootstrap Canvas에 배치. DontDestroyOnLoad.
    // GameNetworkManager.OnUnexpectedDisconnect 구독 — 예기치 않은 연결 종료 시 오버레이 표시.
    //
    // 필수 자식 구조:
    //   Panel                  (GameObject — 오버레이 루트)
    //   Panel/MessageText      (TextMeshProUGUI)
    //   Panel/ConfirmButton    (Button — "확인" 클릭 시 MainMenu 복귀)
    public class NetworkErrorDialog : MonoBehaviour
    {
        public static NetworkErrorDialog Instance { get; private set; }

        [SerializeField] private GameObject      panel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button          confirmButton;
        [SerializeField] private string          defaultMessage  = "서버 연결이 끊어졌습니다.";
        [SerializeField] private string          mainMenuScene   = "MainMenu";

        private bool isSubscribed;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            // Bootstrap Canvas의 자식이라 gameObject 자신은 루트가 아님 — ServerAdminUI와 동일하게
            // transform.root를 영속화한다(그렇지 않으면 "DontDestroyOnLoad only works for root
            // GameObjects" 에러가 나고 다음 씬 로드 때 조용히 파괴된다).
            DontDestroyOnLoad(transform.root.gameObject);

            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);

            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        private void Update()
        {
            if (!isSubscribed && GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.OnUnexpectedDisconnect += OnUnexpectedDisconnect;
                isSubscribed = true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null && Keyboard.current[Key.F8].wasPressedThisFrame)
                Show(string.IsNullOrEmpty(defaultMessage) ? "(디버그) 서버 연결이 끊어졌습니다." : $"[F8 DEBUG]\n{defaultMessage}");
#endif
        }

        private void Unsubscribe()
        {
            if (!isSubscribed) return;
            if (GameNetworkManager.Instance != null)
                GameNetworkManager.Instance.OnUnexpectedDisconnect -= OnUnexpectedDisconnect;
            isSubscribed = false;
        }

        private void OnUnexpectedDisconnect(string reason)
        {
            string msg = string.IsNullOrEmpty(reason) ? defaultMessage : reason;
            Show(msg);
        }

        private void OnConfirmClicked()
        {
            Hide();
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadSceneLocal(mainMenuScene);
        }

        private void Show(string message)
        {
            if (messageText != null) messageText.text = message;
            if (panel != null) panel.SetActive(true);
        }

        private void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
