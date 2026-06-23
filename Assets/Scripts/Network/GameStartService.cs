using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vamsurlike.Network
{
    internal sealed class GameStartService : IGameStartService
    {
        private const string StartGameRequestMessage = "Vamsurlike.StartGameRequest";
        private const byte StartGameRequestCode = 1;

        private readonly NetworkManager networkManager;
        private readonly ILobbyHostService lobbyHostService;
        private readonly string lobbySceneName;
        private readonly string stageSceneName;

        private bool isGameStartRequested;
        private bool isMessageHandlerRegistered;

        public GameStartService(
            NetworkManager networkManager,
            ILobbyHostService lobbyHostService,
            string lobbySceneName,
            string stageSceneName)
        {
            this.networkManager = networkManager;
            this.lobbyHostService = lobbyHostService;
            this.lobbySceneName = lobbySceneName;
            this.stageSceneName = stageSceneName;
        }

        public void RegisterMessageHandler()
        {
            if (isMessageHandlerRegistered || networkManager?.CustomMessagingManager == null) return;

            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                StartGameRequestMessage,
                HandleStartGameRequestMessage);
            isMessageHandlerRegistered = true;
        }

        public void UnregisterMessageHandler()
        {
            if (!isMessageHandlerRegistered || networkManager?.CustomMessagingManager == null) return;

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(StartGameRequestMessage);
            isMessageHandlerRegistered = false;
        }

        public bool RequestStartGame()
        {
            if (networkManager == null || !networkManager.IsConnectedClient || networkManager.CustomMessagingManager == null)
            {
                Debug.LogWarning($"[{nameof(GameStartService)}] 서버에 연결되지 않아 게임 시작을 요청할 수 없습니다.");
                return false;
            }

            if (lobbyHostService == null || !lobbyHostService.IsLocalLobbyHost)
            {
                Debug.LogWarning($"[{nameof(GameStartService)}] 방장만 게임 시작을 요청할 수 있습니다.");
                return false;
            }

            using FastBufferWriter writer = new(sizeof(byte), Allocator.Temp);
            writer.WriteValueSafe(StartGameRequestCode);
            networkManager.CustomMessagingManager.SendNamedMessage(
                StartGameRequestMessage,
                NetworkManager.ServerClientId,
                writer);
            Debug.Log($"[{nameof(GameStartService)}] 게임 시작 요청 전송. clientId={networkManager.LocalClientId}");
            return true;
        }

        public void Dispose()
        {
            UnregisterMessageHandler();
        }

        private void HandleStartGameRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer) return;

            try
            {
                reader.ReadValueSafe(out byte requestCode);
                if (requestCode != StartGameRequestCode)
                {
                    Debug.LogWarning($"[{nameof(GameStartService)}] 유효하지 않은 게임 시작 요청입니다.");
                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{nameof(GameStartService)}] 게임 시작 요청을 읽지 못했습니다: {exception.Message}");
                return;
            }

            if (lobbyHostService == null || senderClientId != lobbyHostService.LobbyHostClientId)
            {
                Debug.LogWarning(
                    $"[{nameof(GameStartService)}] 비방장 게임 시작 요청 거부. " +
                    $"sender={senderClientId}, host={lobbyHostService?.LobbyHostClientId}");
                return;
            }

            if (networkManager.ConnectedClientsIds.Count <= 0)
            {
                Debug.LogWarning($"[{nameof(GameStartService)}] 접속 플레이어가 없어 게임 시작 요청을 거부했습니다.");
                return;
            }

            if (isGameStartRequested || SceneManager.GetActiveScene().name != lobbySceneName)
            {
                Debug.LogWarning($"[{nameof(GameStartService)}] 게임 시작이 이미 진행 중이거나 로비 상태가 아닙니다.");
                return;
            }

            isGameStartRequested = true;
            ServerConsoleLogger.Log($"방장 Client {senderClientId}의 게임 시작 요청 승인");
            networkManager.SceneManager.LoadScene(stageSceneName, LoadSceneMode.Single);
        }
    }
}
