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
    //   ColorPaletteArea/          (Button × 9 — 버튼 Image 자체가 스와치)
    //   StartGameButton            (Button)   — 방장에게만 표시
    //   WaitingText                (TextMeshProUGUI) — 비방장에게만 표시
    //   DisconnectButton           (Button)
    //   LobbySlotUI × 4            (GetComponentsInChildren 자동 탐색)
    public class LobbyUI : MonoBehaviour
    {
        private LobbySlotUI[]    playerSlots;
        private Button[]         colorButtons;
        private TextMeshProUGUI  serverIpText;
        private Button           startGameButton;
        private TextMeshProUGUI  waitingText;
        private Button           disconnectButton;
        private Button           shopButton;
        private PermanentUpgradeShopUI shopPanel;

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
                Transform copyButton = ipRow.Find("CopyButton");
                if (copyButton != null)
                    Destroy(copyButton.gameObject);
            }

            startGameButton = transform.Find("StartGameButton")?.GetComponent<Button>();
            if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGame);

            waitingText = transform.Find("WaitingText")?.GetComponent<TextMeshProUGUI>();

            disconnectButton = transform.Find("DisconnectButton")?.GetComponent<Button>();
            if (disconnectButton != null) disconnectButton.onClick.AddListener(OnDisconnect);

            BindShopObjects();
        }

        private void OnEnable()
        {
            LobbyPlayerState.Changed += Refresh;
            if (GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.OnClientConnected    += OnClientChanged;
                GameNetworkManager.Instance.OnClientDisconnected += OnClientChanged;
                GameNetworkManager.Instance.OnLobbyHostChanged   += OnHostChanged;
            }
            Refresh();
        }

        private void OnDisable()
        {
            LobbyPlayerState.Changed -= Refresh;
            if (GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.OnClientConnected    -= OnClientChanged;
                GameNetworkManager.Instance.OnClientDisconnected -= OnClientChanged;
                GameNetworkManager.Instance.OnLobbyHostChanged   -= OnHostChanged;
            }
            if (playerSlots != null)
                foreach (var slot in playerSlots) slot?.Unbind();
            SetShopVisible(false);
        }

        private void BindShopObjects()
        {
            shopButton = FindSceneComponent<Button>("LobbyShopButton");
            shopPanel = FindSceneComponent<PermanentUpgradeShopUI>("PermanentUpgradeShop");

            if (shopButton != null)
            {
                shopButton.onClick.RemoveListener(OpenShop);
                shopButton.onClick.AddListener(OpenShop);
                shopButton.gameObject.SetActive(false);
            }

            if (shopPanel != null)
                shopPanel.gameObject.SetActive(false);
        }

        private static T FindSceneComponent<T>(string objectName) where T : Component
        {
            var objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i].name != objectName || !objects[i].scene.IsValid())
                    continue;

                var component = objects[i].GetComponent<T>();
                if (component != null)
                    return component;
            }

            return null;
        }

        private void OpenShop()
        {
            if (shopPanel == null) return;
            shopPanel.OpenPanel();
        }

        private void SetShopVisible(bool visible)
        {
            if (shopButton != null)
                shopButton.gameObject.SetActive(visible);
            if (!visible && shopPanel != null && shopPanel.gameObject.activeSelf)
                shopPanel.gameObject.SetActive(false);
        }

        private void OnClientChanged(ulong _) => Refresh();
        private void OnHostChanged(ulong _)   => Refresh();

        private void Refresh()
        {
            RefreshSlots();
            RefreshServerIp();

            bool isHost = GameNetworkManager.Instance?.IsLocalLobbyHost ?? false;
            if (startGameButton != null) startGameButton.gameObject.SetActive(isHost);
            if (waitingText     != null) waitingText.gameObject.SetActive(!isHost);
            SetShopVisible(true);

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
                state.RequestColorIndex(index);
        }

        private void OnStartGame()  => GameNetworkManager.Instance?.RequestStartGame();
        private void OnDisconnect() => GameNetworkManager.Instance?.Disconnect();

    }
}
