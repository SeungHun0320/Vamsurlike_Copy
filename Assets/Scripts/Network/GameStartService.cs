using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vamsurlike.Network
{
    internal sealed class GameStartService : IGameStartService
    {
        private const string StartGameRequestMessage    = "Vamsurlike.StartGameRequest";
        private const string ReturnToLobbyRequestMessage = "Vamsurlike.ReturnToLobbyRequest";
        private const byte StartGameRequestCode    = 1;
        private const byte ReturnToLobbyRequestCode = 2;

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
                StartGameRequestMessage, HandleStartGameRequestMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                ReturnToLobbyRequestMessage, HandleReturnToLobbyRequestMessage);
            isMessageHandlerRegistered = true;
        }

        public void UnregisterMessageHandler()
        {
            if (!isMessageHandlerRegistered || networkManager?.CustomMessagingManager == null) return;

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(StartGameRequestMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ReturnToLobbyRequestMessage);
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

        public bool RequestReturnToLobby()
        {
            if (networkManager == null) return false;

            // 서버면 직접 처리
            if (networkManager.IsServer)
            {
                ExecuteReturnToLobby();
                return true;
            }

            // 클라이언트면 서버에 요청
            if (!networkManager.IsConnectedClient || networkManager.CustomMessagingManager == null)
            {
                Debug.LogWarning($"[{nameof(GameStartService)}] 서버에 연결되지 않아 로비 복귀를 요청할 수 없습니다.");
                return false;
            }

            using FastBufferWriter writer = new(sizeof(byte), Allocator.Temp);
            writer.WriteValueSafe(ReturnToLobbyRequestCode);
            networkManager.CustomMessagingManager.SendNamedMessage(
                ReturnToLobbyRequestMessage,
                NetworkManager.ServerClientId,
                writer);
            Debug.Log($"[{nameof(GameStartService)}] 로비 복귀 요청 전송. clientId={networkManager.LocalClientId}");
            return true;
        }

        public void Dispose()
        {
            UnregisterMessageHandler();
        }

        private void HandleReturnToLobbyRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer) return;

            ServerConsoleLogger.Log($"[검증] 로비 복귀 요청 수신 — sender={senderClientId}");

            byte requestCode;
            try
            {
                reader.ReadValueSafe(out requestCode);
            }
            catch (Exception exception)
            {
                ServerConsoleLogger.Log($"[검증] 로비 복귀 요청 파싱 실패: {exception.Message}");
                return;
            }

            if (requestCode != ReturnToLobbyRequestCode)
            {
                ServerConsoleLogger.Log($"[검증] 로비 복귀 요청 코드 불일치 — expected={ReturnToLobbyRequestCode}, got={requestCode} → 거부");
                return;
            }

            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene == lobbySceneName)
            {
                ServerConsoleLogger.Log($"[검증] 이미 로비 씬({lobbySceneName})입니다 — 무시");
                return;
            }

            ServerConsoleLogger.Log($"[검증] 로비 복귀 승인 — sender={senderClientId}, currentScene={currentScene}");
            ExecuteReturnToLobby();
        }

        private void ExecuteReturnToLobby()
        {
            ServerConsoleLogger.Log($"로비 복귀 실행 → '{lobbySceneName}' 로드");
            isGameStartRequested = false;
            networkManager.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }

        private void HandleStartGameRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer) return;

            ServerConsoleLogger.Log($"[검증] 게임 시작 요청 수신 — sender={senderClientId}");

            byte requestCode;
            try
            {
                reader.ReadValueSafe(out requestCode);
            }
            catch (Exception exception)
            {
                ServerConsoleLogger.Log($"[검증] 게임 시작 요청 파싱 실패: {exception.Message}");
                return;
            }

            if (requestCode != StartGameRequestCode)
            {
                ServerConsoleLogger.Log($"[검증] 요청 코드 불일치 — expected={StartGameRequestCode}, got={requestCode} → 거부");
                return;
            }

            ulong expectedHost = lobbyHostService?.LobbyHostClientId ?? NoHost;
            if (lobbyHostService == null || senderClientId != expectedHost)
            {
                ServerConsoleLogger.Log($"[검증] 방장 아님 → 거부 (sender={senderClientId}, host={expectedHost})");
                return;
            }

            int playerCount = networkManager.ConnectedClientsIds.Count;
            if (playerCount <= 0)
            {
                ServerConsoleLogger.Log($"[검증] 접속 플레이어 없음(count={playerCount}) → 거부");
                return;
            }

            string currentScene = SceneManager.GetActiveScene().name;
            if (isGameStartRequested)
            {
                ServerConsoleLogger.Log($"[검증] 이미 시작 진행 중(isGameStartRequested=true) → 거부");
                return;
            }
            if (currentScene != lobbySceneName)
            {
                ServerConsoleLogger.Log($"[검증] 현재 씬이 로비가 아님(currentScene={currentScene}) → 거부");
                return;
            }

            isGameStartRequested = true;
            ServerConsoleLogger.Log($"[검증] 게임 시작 승인 — host={senderClientId}, players={playerCount} → '{stageSceneName}' 로드");
            networkManager.SceneManager.LoadScene(stageSceneName, LoadSceneMode.Single);
        }

        private const ulong NoHost = ulong.MaxValue;
    }
}
