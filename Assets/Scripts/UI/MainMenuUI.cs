using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vamsurlike.Core;
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
        [SerializeField] private string stageSceneName = "Stage_01";

        private bool isBusy;

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
            {
                relayHostButton.onClick.AddListener(() => _ = OnRelayHostClickedAsync());
                relayHostButton.interactable = NetworkBootstrapper.IsUgsReady;
                if (!NetworkBootstrapper.IsUgsReady)
                    NetworkBootstrapper.OnUgsReady += EnableRelayButton;
            }
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

        private void OnDestroy()
        {
            // 씬 파괴 전 static event 구독 해제 (죽은 UI 참조 방지)
            NetworkBootstrapper.OnUgsReady -= EnableRelayButton;
        }

        private void EnableRelayButton()
        {
            NetworkBootstrapper.OnUgsReady -= EnableRelayButton;
            if (relayHostButton != null)
                relayHostButton.interactable = true;
        }

        // 로컬 IP 직접 호스트 (MPM / LAN 테스트)
        private void OnLocalHostClicked()
        {
            if (isBusy) return;
            SetStatus("로컬 호스트 시작 중...");
            var gnm = GameNetworkManager.Instance;
            bool ok = gnm != null && gnm.StartAsHost();
            SetStatus(ok ? $"호스트 대기 중 — {gnm.CurrentIp}:{gnm.CurrentPort}" : "호스트 시작 실패.");
        }

        // Relay 세션 생성 후 호스트
        private async Task OnRelayHostClickedAsync()
        {
            if (isBusy) return;
            isBusy = true;
            try
            {
                var gnm = GameNetworkManager.Instance;
                if (gnm == null || !gnm.IsAvailableToStart)
                {
                    SetStatus(gnm == null ? "GameNetworkManager를 찾을 수 없습니다." : "이미 네트워크가 실행 중입니다.");
                    return;
                }

                var relay = RelayManager.Instance;
                if (relay == null)
                {
                    SetStatus("RelayManager를 찾을 수 없습니다.");
                    return;
                }

                SetStatus("Relay 세션 생성 중...");
                string code = await relay.CreateSessionAsync(4);
                if (code == null)
                {
                    SetStatus("Relay 세션 생성 실패. 로컬 호스트를 사용하세요.");
                    return;
                }

                if (!gnm.StartAsRelayHost())
                {
                    SetStatus("호스트 시작 실패.");
                    await relay.LeaveSessionAsync();
                    return;
                }
                ShowSessionCode(code);
                SetStatus($"호스트 대기 중. 코드: {code}");
            }
            finally
            {
                isBusy = false;
            }
        }

        // 코드(Relay) 또는 IP(LAN)로 참여
        private async Task OnJoinClickedAsync()
        {
            if (isBusy) return;

            string input = ipOrCodeInput != null ? ipOrCodeInput.text.Trim() : "";

            if (string.IsNullOrEmpty(input))
            {
                SetStatus("IP 또는 세션 코드를 입력하세요.");
                return;
            }

            isBusy = true;
            try
            {
                if (TryParseLocalEndpoint(input, out string ip, out ushort port))
                {
                    SetStatus($"로컬 접속 중: {ip}:{port}");
                    var gnm = GameNetworkManager.Instance;
                    if (gnm == null || !gnm.StartAsClient(ip, port))
                    {
                        SetStatus("클라이언트 시작 실패.");
                        return;
                    }
                    await WaitForConnectionAsync(timeoutSeconds: 5f);
                    return;
                }

                // 8자리 이하이고 점이 없으면 → Relay 코드, 그 외 → IP 주소
                if (input.Length <= 8 && !input.Contains("."))
                {
                    // UGS 미준비 상태에서 Relay 코드 시도 차단 (IP 접속은 허용)
                    if (!NetworkBootstrapper.IsUgsReady)
                    {
                        SetStatus("UGS 초기화 중입니다. 잠시 후 다시 시도하세요.");
                        return;
                    }

                    var relay = RelayManager.Instance;
                    if (relay == null)
                    {
                        SetStatus("RelayManager를 찾을 수 없습니다.");
                        return;
                    }

                    SetStatus($"Relay 세션 참여 중: {input}");
                    bool success = await relay.JoinSessionAsync(input);
                    if (!success)
                    {
                        SetStatus("세션 참여 실패.");
                        return;
                    }

                    var gnm = GameNetworkManager.Instance;
                    if (gnm == null || !gnm.StartAsRelayClient())
                    {
                        SetStatus("클라이언트 시작 실패.");
                        await relay.LeaveSessionAsync();
                        return;
                    }
                    if (!await WaitForConnectionAsync(timeoutSeconds: 5f))
                        await relay.LeaveSessionAsync();
                }
                else
                {
                    SetStatus($"로컬 접속 중: {input}:7777");
                    var gnm = GameNetworkManager.Instance;
                    if (gnm == null || !gnm.StartAsClient(input))
                    {
                        SetStatus("클라이언트 시작 실패.");
                        return;
                    }
                    await WaitForConnectionAsync(timeoutSeconds: 5f);
                }
            }
            finally
            {
                isBusy = false;
            }
        }

        // 솔로/게임 시작: 네트워크가 없으면 혼자 시작, 호스트 대기 중이면 현재 세션을 시작
        private void OnSoloClicked()
        {
            if (isBusy) return;

            var gnm = GameNetworkManager.Instance;
            if (gnm == null)
            {
                SetStatus("GameNetworkManager를 찾을 수 없습니다.");
                return;
            }

            if (!gnm.IsListening)
            {
                SetStatus("솔로 시작...");
                if (!gnm.StartAsHost())
                {
                    SetStatus("솔로 시작 실패.");
                    return;
                }

                TryLoadStageNetwork();
                return;
            }

            if (gnm.IsServer)
            {
                SetStatus("게임 시작...");
                TryLoadStageNetwork();
                return;
            }

            SetStatus("클라이언트는 게임을 시작할 수 없습니다.");
        }

        private async Task<bool> WaitForConnectionAsync(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.IsClientConnected)
                {
                    SetStatus($"접속 완료 — 플레이어 {GameNetworkManager.Instance.ConnectedPlayerCount}명");
                    return true;
                }
                await Task.Delay(200);
                elapsed += 0.2f;
            }
            SetStatus("접속 실패: 호스트를 찾을 수 없습니다.");
            GameNetworkManager.Instance?.Disconnect();
            return false;
        }

        private void TryLoadStageNetwork()
        {
            if (SceneLoader.Instance == null)
            {
                SetStatus("SceneLoader를 찾을 수 없습니다.");
                return;
            }

            SceneLoader.Instance.LoadSceneNetwork(stageSceneName);
        }

        private static bool TryParseLocalEndpoint(string input, out string ip, out ushort port)
        {
            const ushort defaultPort = 7777;

            ip = input;
            port = defaultPort;

            if (ushort.TryParse(input, out ushort portOnly))
            {
                ip = "127.0.0.1";
                port = portOnly;
                return true;
            }

            int separatorIndex = input.LastIndexOf(':');
            if (separatorIndex <= 0 || separatorIndex >= input.Length - 1)
                return input.Contains(".");

            string portText = input[(separatorIndex + 1)..];
            if (!ushort.TryParse(portText, out ushort parsedPort))
                return false;

            ip = input[..separatorIndex];
            port = parsedPort;
            return true;
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
