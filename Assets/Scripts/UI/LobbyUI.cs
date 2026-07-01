using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vamsurlike.Network;
using Vamsurlike.Player;

namespace Vamsurlike.UI
{
    // 접속 후 대기실 패널. MainMenuUI가 isConnected 상태에 따라 gameObject.SetActive()를 제어한다.
    // 자식 이름 규칙으로 컴포넌트를 자동 탐색하므로 Inspector 와이어링 불필요.
    //
    // 필수 자식 구조:
    //   ServerIPRow/ServerIPText   (TextMeshProUGUI)
    //   ServerIPRow/CopyButton     (Button)
    //   ColorPaletteArea/          (Button × 8 — 버튼 Image 자체가 스와치)
    //   StartGameButton            (Button)
    //   LobbySlotUI × 4            (GetComponentsInChildren 자동 탐색)
    public class LobbyUI : MonoBehaviour
    {
        private LobbySlotUI[]    playerSlots;
        private Button[]         colorButtons;
        private TextMeshProUGUI  serverIpText;
        private Button           copyIpButton;
        private Button           startGameButton;

        private void Awake()
        {
            playerSlots     = GetComponentsInChildren<LobbySlotUI>(includeInactive: true);

            var palette = transform.Find("ColorPaletteArea");
            if (palette != null)
            {
                colorButtons = palette.GetComponentsInChildren<Button>();
                for (int i = 0; i < colorButtons.Length && i < PlayerColorSync.Palette.Length; i++)
                {
                    var img = colorButtons[i].GetComponent<Image>();
                    if (img != null) img.color = PlayerColorSync.Palette[i];

                    int idx = i;
                    colorButtons[i].onClick.AddListener(() => OnColorPicked(idx));
                }
            }

            var ipRow = transform.Find("ServerIPRow");
            if (ipRow != null)
            {
                serverIpText = ipRow.Find("ServerIPText")?.GetComponent<TextMeshProUGUI>();
                copyIpButton = ipRow.Find("CopyButton")?.GetComponent<Button>();
                if (copyIpButton != null) copyIpButton.onClick.AddListener(CopyIp);
            }

            startGameButton = transform.Find("StartGameButton")?.GetComponent<Button>();
            if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGame);
        }

        private void OnEnable()
        {
            if (GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.OnClientConnected    += OnClientChanged;
                GameNetworkManager.Instance.OnClientDisconnected += OnClientChanged;
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.OnClientConnected    -= OnClientChanged;
                GameNetworkManager.Instance.OnClientDisconnected -= OnClientChanged;
            }
            if (playerSlots != null)
                foreach (var slot in playerSlots) slot?.Unbind();
        }

        private void OnClientChanged(ulong _) => Refresh();

        private void Refresh()
        {
            RefreshSlots();
            RefreshServerIp();

            bool isHost = GameNetworkManager.Instance?.IsLocalLobbyHost ?? false;
            if (startGameButton != null)
                startGameButton.gameObject.SetActive(isHost);

            bool canPick = NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsConnectedClient
                && LobbyPlayerState.All.ContainsKey(NetworkManager.Singleton.LocalClientId);
            if (colorButtons != null)
                foreach (var btn in colorButtons)
                    if (btn != null) btn.interactable = canPick;
        }

        private void RefreshSlots()
        {
            if (playerSlots == null) return;
            foreach (var slot in playerSlots) slot?.Unbind();

            var states = LobbyPlayerState.All.Values
                .OrderBy(s => s.OwnerClientId)
                .ToArray();

            for (int i = 0; i < playerSlots.Length; i++)
            {
                if (playerSlots[i] == null) continue;
                if (i < states.Length) playerSlots[i].Bind(states[i]);
            }
        }

        private void RefreshServerIp()
        {
            if (serverIpText == null) return;
            var gnm = GameNetworkManager.Instance;
            serverIpText.text = gnm != null ? $"{gnm.CurrentIp}:{gnm.CurrentPort}" : "";
        }

        private void OnColorPicked(int index)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient) return;
            ulong local = NetworkManager.Singleton.LocalClientId;
            if (LobbyPlayerState.All.TryGetValue(local, out var state) && state.IsOwner)
                state.ColorIndex.Value = index;
        }

        private void OnStartGame() => GameNetworkManager.Instance?.RequestStartGame();

        private void CopyIp()
        {
            if (serverIpText != null)
                GUIUtility.systemCopyBuffer = serverIpText.text;
        }
    }
}
