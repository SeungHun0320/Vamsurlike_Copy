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
        // 서버 모드 정리 시에도 꺼지면 안 되는 형제 오브젝트(설정 버튼/패널 등) — 이름으로 매칭.
        [SerializeField] private string[] preserveSiblingNames = { "SettingsButton", "SettingsPanel" };
        [SerializeField, Min(10)] private int maxVisibleLogLines = 500;
        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new(1520f, 1040f);
        [SerializeField] private Vector2 logViewAnchor = new(0.5f, 0.5f);
        [SerializeField] private Vector2 logViewPosition = new(0f, -80f);
        [SerializeField] private Vector2 logViewSize = new(1400f, 680f);
        [SerializeField] private Vector2 logTextPaddingMin = new(28f, 20f);
        [SerializeField] private Vector2 logTextPaddingMax = new(-28f, -20f);
        [SerializeField] private Vector2 playerCountPosition = new(0f, 420f);
        [SerializeField, Min(1)] private int disableCameraFramesOnSceneLoad = 5;
        [Header("Style")]
        [SerializeField] private Color logBackgroundColor = new(0.035f, 0.045f, 0.06f, 0.94f);
        [SerializeField] private Color logTextColor = new(0.82f, 0.9f, 1f);
        [SerializeField, Min(1f)] private float logFontSize = 32f;
        [SerializeField, Min(0.1f)] private float existingTextScale = 2f;
        [SerializeField, Min(1f)] private float logScrollSensitivity = 48f;
        [Header("Labels")]
        [SerializeField] private string playerCountFormat = "접속 중: {0}명";
        [SerializeField] private string sceneLoadedFormat = "{0:HH:mm:ss} [SERVER] 씬 '{1}' 로드";
        [SerializeField] private string readyMessageFormat = "{0:HH:mm:ss} [SERVER] 관리 화면 준비 완료";

        private static ServerAdminUI s_persistent;

        private readonly Queue<string> visibleLogs = new();
        private TextMeshProUGUI logText;
        private RectTransform logContentRect;
        private ScrollRect logScrollRect;

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

            if (panel == null)
            {
                Debug.LogError($"[{nameof(ServerAdminUI)}] panel 참조가 없습니다.", this);
                enabled = false;
                return;
            }

            // panel 외 Canvas의 모든 직접 자식 비활성화 (배경/타이틀 등 잔여 요소 제거)
            // 단, preserveSiblingNames에 등록된 것(설정 버튼/패널 등)은 서버 모드에서도 그대로 둔다.
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (child == panel) continue;
                if (System.Array.IndexOf(preserveSiblingNames, child.name) >= 0) continue;

                child.SetActive(false);
            }

            s_persistent = this;
            DontDestroyOnLoad(transform.root.gameObject);
            ScaleExistingPanelText();
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
                playerCountText.text = string.Format(playerCountFormat, count);
        }

        // 스테이지 씬 로드 완료 시 카메라를 여러 프레임 동안 강제 비활성화
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu") return;
            _disableCameraFrames = disableCameraFramesOnSceneLoad;
            AppendLog(string.Format(sceneLoadedFormat, System.DateTime.Now, scene.name));
        }

        private void CreateLogView()
        {
            if (panel == null)
            {
                Debug.LogError($"[{nameof(ServerAdminUI)}] panel 참조가 없습니다.", this);
                return;
            }

            if (panel.transform is RectTransform panelRect)
                panelRect.sizeDelta = panelSize;

            GameObject background = new("ServerLogView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            background.transform.SetParent(panel.transform, false);

            RectTransform backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = logViewAnchor;
            backgroundRect.anchorMax = logViewAnchor;
            backgroundRect.anchoredPosition = logViewPosition;
            backgroundRect.sizeDelta = logViewSize;

            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = logBackgroundColor;
            backgroundImage.raycastTarget = true;

            GameObject viewport = new("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(background.transform, false);

            RectTransform viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;

            GameObject content = new("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);

            logContentRect = (RectTransform)content.transform;
            logContentRect.anchorMin = new Vector2(0f, 1f);
            logContentRect.anchorMax = new Vector2(1f, 1f);
            logContentRect.pivot = new Vector2(0.5f, 1f);
            logContentRect.anchoredPosition = Vector2.zero;
            logContentRect.sizeDelta = Vector2.zero;

            GameObject textObject = new("LogText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(content.transform, false);

            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.offsetMin = logTextPaddingMin;
            textRect.offsetMax = logTextPaddingMax;

            logText = textObject.GetComponent<TextMeshProUGUI>();
            if (playerCountText != null) logText.font = playerCountText.font;
            logText.fontSize = logFontSize;
            logText.color = logTextColor;
            logText.alignment = TextAlignmentOptions.TopLeft;
            logText.textWrappingMode = TextWrappingModes.NoWrap;
            logText.overflowMode = TextOverflowModes.Overflow;
            logText.raycastTarget = false;

            logScrollRect = background.GetComponent<ScrollRect>();
            logScrollRect.viewport = viewportRect;
            logScrollRect.content = logContentRect;
            logScrollRect.horizontal = false;
            logScrollRect.vertical = true;
            logScrollRect.movementType = ScrollRect.MovementType.Clamped;
            logScrollRect.scrollSensitivity = logScrollSensitivity;

            if (playerCountText != null)
                playerCountText.rectTransform.anchoredPosition = playerCountPosition;
        }

        private void RestoreBufferedLogs()
        {
            IReadOnlyList<string> entries = ServerConsoleLogger.Entries;
            int startIndex = Mathf.Max(0, entries.Count - maxVisibleLogLines);
            for (int i = startIndex; i < entries.Count; i++)
                AppendLog(entries[i]);

            if (visibleLogs.Count == 0)
                AppendLog(string.Format(readyMessageFormat, System.DateTime.Now));
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
            RefreshLogContentSize();
            logScrollRect.verticalNormalizedPosition = 0f;
        }

        private void ScaleExistingPanelText()
        {
            if (panel == null || Mathf.Approximately(existingTextScale, 1f)) return;

            foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true))
                text.fontSize *= existingTextScale;
        }

        private void RefreshLogContentSize()
        {
            if (logText == null || logContentRect == null) return;

            float height = Mathf.Max(logViewSize.y, logText.preferredHeight + logTextPaddingMin.y - logTextPaddingMax.y);
            logContentRect.sizeDelta = new Vector2(0f, height);

            RectTransform textRect = logText.rectTransform;
            textRect.sizeDelta = new Vector2(0f, height - logTextPaddingMin.y + logTextPaddingMax.y);
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
