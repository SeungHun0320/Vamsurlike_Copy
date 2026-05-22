using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vamsurlike.Network;

namespace Vamsurlike.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button hostButton;
        [SerializeField] private Button relayHostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button soloButton;
        [SerializeField] private TMP_InputField ipOrCodeInput;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI sessionCodeText;

        private void Awake()
        {
            if (hostButton == null || soloButton == null)
            {
                Debug.LogError($"[{nameof(MainMenuUI)}] 필수 버튼 참조가 누락됐습니다.", this);
                enabled = false;
                return;
            }

            hostButton.onClick.AddListener(OnLocalHostClicked);
            soloButton.onClick.AddListener(OnSoloClicked);

            if (relayHostButton != null)
                relayHostButton.onClick.AddListener(() => _ = OnRelayHostClickedAsync());
            if (joinButton != null)
                joinButton.onClick.AddListener(() => _ = OnJoinClickedAsync());

            if (sessionCodeText != null)
                sessionCodeText.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (GameNetworkManager.Instance == null) return;
            GameNetworkManager.Instance.OnClientConnected    += HandlePlayerCountChanged;
            GameNetworkManager.Instance.OnClientDisconnected += HandlePlayerCountChanged;
        }

        private void OnDisable()
        {
            if (GameNetworkManager.Instance == null) return;
            GameNetworkManager.Instance.OnClientConnected    -= HandlePlayerCountChanged;
            GameNetworkManager.Instance.OnClientDisconnected -= HandlePlayerCountChanged;
        }

        // 로컬 IP 직접 호스트 (MPM / LAN 테스트)
        private void OnLocalHostClicked()
        {
            SetStatus("로컬 호스트 시작 중...");
            GameNetworkManager.Instance?.StartAsHost();
            SetStatus("호스트 대기 중 — 127.0.0.1:7777");
        }

        // Relay 세션 생성 후 호스트
        private async Task OnRelayHostClickedAsync()
        {
            SetStatus("Relay 세션 생성 중...");
            string code = await RelayManager.Instance?.CreateSessionAsync(4);
            if (code == null)
            {
                SetStatus("Relay 세션 생성 실패. 로컬 호스트를 사용하세요.");
                return;
            }

            GameNetworkManager.Instance?.StartAsHost();
            ShowSessionCode(code);
            SetStatus($"호스트 대기 중. 코드: {code}");
        }

        // 코드(Relay) 또는 IP(LAN)로 참여
        private async Task OnJoinClickedAsync()
        {
            string input = ipOrCodeInput != null ? ipOrCodeInput.text.Trim() : "";

            if (string.IsNullOrEmpty(input))
            {
                SetStatus("IP 또는 세션 코드를 입력하세요.");
                return;
            }

            // 6자리 이하 → Relay 코드, 그 이상 → IP 주소로 판단
            if (input.Length <= 8 && !input.Contains("."))
            {
                SetStatus($"Relay 세션 참여 중: {input}");
                bool success = await RelayManager.Instance?.JoinSessionAsync(input);
                if (!success)
                {
                    SetStatus("세션 참여 실패.");
                    return;
                }
                GameNetworkManager.Instance?.StartAsClient();
            }
            else
            {
                SetStatus($"로컬 접속 중: {input}");
                GameNetworkManager.Instance?.StartAsClient(input);
                await WaitForConnectionAsync(timeoutSeconds: 5f);
            }
        }

        // 솔로: 로컬 호스트로 바로 게임 시작
        private void OnSoloClicked()
        {
            SetStatus("솔로 시작...");
            GameNetworkManager.Instance?.StartAsHost();
            // Phase 2에서 SceneLoader.LoadSceneNetwork("Stage_01") 추가 예정
        }

        private async Task WaitForConnectionAsync(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.IsClientConnected)
                {
                    SetStatus($"접속 완료 — 플레이어 {GameNetworkManager.Instance.ConnectedPlayerCount}명");
                    return;
                }
                await Task.Delay(200);
                elapsed += 0.2f;
            }
            SetStatus("접속 실패: 호스트를 찾을 수 없습니다.");
            GameNetworkManager.Instance?.Disconnect();
        }

        private void HandlePlayerCountChanged(ulong _)
        {
            int count = GameNetworkManager.Instance?.ConnectedPlayerCount ?? 0;
            SetStatus($"플레이어 {count}명 접속 중");
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
            Debug.Log($"[{nameof(MainMenuUI)}] {message}");
        }

        private void ShowSessionCode(string code)
        {
            if (sessionCodeText == null) return;
            sessionCodeText.text = $"세션 코드: {code}";
            sessionCodeText.gameObject.SetActive(true);
        }
    }
}
