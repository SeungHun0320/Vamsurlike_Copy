using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Vamsurlike.Network;

namespace Vamsurlike.UI
{
    public class ServerAdminUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField, Min(10)] private int maxVisibleLogLines = 24;

        private static ServerAdminUI s_persistent;

        private readonly Queue<string> visibleLogs = new();
        private TextMeshProUGUI logText;

        private void Awake()
        {
            if (!IsServerMode())
            {
                if (panel != null) panel.SetActive(false);
                enabled = false;
                return;
            }

            // MainMenu가 다시 로드되는 등 중복 생성 방지
            if (s_persistent != null)
            {
                Destroy(gameObject);
                return;
            }

            // panel 외 Canvas의 모든 직접 자식 비활성화 (배경/타이틀 등 잔여 요소 제거)
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (child != panel)
                    child.SetActive(false);
            }

            s_persistent = this;
            DontDestroyOnLoad(transform.root.gameObject);
            CreateLogView();
        }

        private int _disableCameraFrames;

        private void Start()
        {
            UpdatePlayerCount();
            RestoreBufferedLogs();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Update()
        {
            if (_disableCameraFrames > 0)
            {
                _disableCameraFrames--;
                foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    cam.enabled = false;
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (s_persistent == this) s_persistent = null;
        }

        private void OnEnable()
        {
            ServerConsoleLogger.OnEntryAdded += AppendLog;

            if (GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.OnClientConnected    += OnPlayerCountChanged;
                GameNetworkManager.Instance.OnClientDisconnected += OnPlayerCountChanged;
            }
        }

        private void OnDisable()
        {
            ServerConsoleLogger.OnEntryAdded -= AppendLog;

            if (GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.OnClientConnected    -= OnPlayerCountChanged;
                GameNetworkManager.Instance.OnClientDisconnected -= OnPlayerCountChanged;
            }
        }

        private void OnPlayerCountChanged(ulong _) => UpdatePlayerCount();

        private void UpdatePlayerCount()
        {
            int count = GameNetworkManager.Instance?.ConnectedPlayerCount ?? 0;
            if (playerCountText != null)
                playerCountText.text = $"접속 중: {count}명";
        }

        // 스테이지 씬 로드 완료 시 카메라를 여러 프레임 동안 강제 비활성화
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu") return;
            _disableCameraFrames = 5; // Start/Awake에서 늦게 활성화되는 카메라까지 처리
            AppendLog($"{System.DateTime.Now:HH:mm:ss} [SERVER] 씬 '{scene.name}' 로드");
        }

        private void CreateLogView()
        {
            if (panel == null)
            {
                Debug.LogError($"[{nameof(ServerAdminUI)}] panel 참조가 없습니다.", this);
                return;
            }

            if (panel.transform is RectTransform panelRect)
                panelRect.sizeDelta = new Vector2(760f, 520f);

            GameObject background = new("ServerLogView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            background.transform.SetParent(panel.transform, false);

            RectTransform backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = new Vector2(0f, -40f);
            backgroundRect.sizeDelta = new Vector2(700f, 340f);

            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0.035f, 0.045f, 0.06f, 0.94f);
            backgroundImage.raycastTarget = false;

            GameObject textObject = new("LogText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(background.transform, false);

            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 10f);
            textRect.offsetMax = new Vector2(-14f, -10f);

            logText = textObject.GetComponent<TextMeshProUGUI>();
            if (playerCountText != null) logText.font = playerCountText.font;
            logText.fontSize = 16f;
            logText.color = new Color(0.82f, 0.9f, 1f);
            logText.alignment = TextAlignmentOptions.TopLeft;
            logText.textWrappingMode = TextWrappingModes.NoWrap;
            logText.overflowMode = TextOverflowModes.Overflow;
            logText.raycastTarget = false;

            if (playerCountText != null)
                playerCountText.rectTransform.anchoredPosition = new Vector2(0f, 210f);
        }

        private void RestoreBufferedLogs()
        {
            IReadOnlyList<string> entries = ServerConsoleLogger.Entries;
            int startIndex = Mathf.Max(0, entries.Count - maxVisibleLogLines);
            for (int i = startIndex; i < entries.Count; i++)
                AppendLog(entries[i]);

            if (visibleLogs.Count == 0)
                AppendLog($"{System.DateTime.Now:HH:mm:ss} [SERVER] 관리 화면 준비 완료");
        }

        private void AppendLog(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return;

            visibleLogs.Enqueue(entry);
            while (visibleLogs.Count > maxVisibleLogLines)
                visibleLogs.Dequeue();

            if (logText == null) return;

            StringBuilder builder = new();
            foreach (string line in visibleLogs)
                builder.AppendLine(line);
            logText.text = builder.ToString();
        }

        private static bool IsServerMode()
        {
            if (NetworkBootstrapper.IsServerBuild) return true;
#if UNITY_EDITOR
            foreach (string tag in Unity.Multiplayer.PlayMode.CurrentPlayer.Tags)
                if (string.Equals(tag, "Server", System.StringComparison.OrdinalIgnoreCase)) return true;
#endif
            return false;
        }
    }
}
