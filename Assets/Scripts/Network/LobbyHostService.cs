using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Network
{
    internal sealed class LobbyHostService : ILobbyHostService
    {
        public const ulong NoLobbyHost = ulong.MaxValue;

        private const string LobbyHostChangedMessage = "Vamsurlike.LobbyHostChanged";
        private const string LobbyHostRequestMessage = "Vamsurlike.LobbyHostRequest";

        private readonly NetworkManager networkManager;
        private bool isMessageHandlerRegistered;

        public event Action<ulong> LobbyHostChanged;

        public ulong LobbyHostClientId { get; private set; } = NoLobbyHost;
        public bool IsLocalLobbyHost =>
            networkManager != null
            && networkManager.IsClient
            && networkManager.LocalClientId == LobbyHostClientId;

        public LobbyHostService(NetworkManager networkManager)
        {
            this.networkManager = networkManager;
        }

        public void RegisterMessageHandler()
        {
            if (isMessageHandlerRegistered || networkManager?.CustomMessagingManager == null) return;

            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                LobbyHostChangedMessage,
                HandleLobbyHostChangedMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                LobbyHostRequestMessage,
                HandleLobbyHostRequestMessage);
            isMessageHandlerRegistered = true;

            if (networkManager.IsClient && !networkManager.IsServer)
                RequestLobbyHostFromServer();
        }

        public void UnregisterMessageHandler()
        {
            if (!isMessageHandlerRegistered || networkManager?.CustomMessagingManager == null) return;

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(LobbyHostChangedMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(LobbyHostRequestMessage);
            isMessageHandlerRegistered = false;
        }

        public void HandleClientConnected(ulong clientId)
        {
            if (networkManager == null) return;
            if (!networkManager.IsServer)
            {
                if (networkManager.IsClient && clientId == networkManager.LocalClientId)
                    RequestLobbyHostFromServer();
                return;
            }

            if (LobbyHostClientId == NoLobbyHost)
            {
                SetLobbyHost(clientId);
                ServerConsoleLogger.Log($"Client {clientId} 방장 지정");
            }

            BroadcastLobbyHost();
        }

        public void HandleClientDisconnected(ulong clientId)
        {
            if (networkManager == null || !networkManager.IsServer || clientId != LobbyHostClientId) return;

            SetLobbyHost(FindNextLobbyHost(clientId));
            BroadcastLobbyHost();

            string message = LobbyHostClientId == NoLobbyHost
                ? "접속 플레이어 없음 - 방장 해제"
                : $"Client {LobbyHostClientId} 새 방장 지정";
            ServerConsoleLogger.Log(message);
        }

        public void Clear()
        {
            SetLobbyHost(NoLobbyHost);
        }

        public void Dispose()
        {
            UnregisterMessageHandler();
            LobbyHostChanged = null;
        }

        private ulong FindNextLobbyHost(ulong disconnectedClientId)
        {
            ulong nextHost = NoLobbyHost;
            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (clientId == disconnectedClientId) continue;
                if (nextHost == NoLobbyHost || clientId < nextHost)
                    nextHost = clientId;
            }

            return nextHost;
        }

        private void BroadcastLobbyHost()
        {
            if (networkManager == null || !networkManager.IsServer || networkManager.CustomMessagingManager == null)
                return;
            if (networkManager.ConnectedClientsIds.Count == 0) return;

            using FastBufferWriter writer = new(sizeof(ulong), Allocator.Temp);
            writer.WriteValueSafe(LobbyHostClientId);
            networkManager.CustomMessagingManager.SendNamedMessageToAll(
                LobbyHostChangedMessage,
                writer);
        }

        private void SendLobbyHostToClient(ulong clientId)
        {
            if (networkManager == null || !networkManager.IsServer || networkManager.CustomMessagingManager == null)
                return;

            using FastBufferWriter writer = new(sizeof(ulong), Allocator.Temp);
            writer.WriteValueSafe(LobbyHostClientId);
            networkManager.CustomMessagingManager.SendNamedMessage(
                LobbyHostChangedMessage,
                clientId,
                writer);
        }

        private void RequestLobbyHostFromServer()
        {
            if (networkManager?.CustomMessagingManager == null || !networkManager.IsConnectedClient)
                return;

            using FastBufferWriter writer = new(1, Allocator.Temp);
            writer.WriteValueSafe((byte)0);
            networkManager.CustomMessagingManager.SendNamedMessage(
                LobbyHostRequestMessage,
                NetworkManager.ServerClientId,
                writer);
        }

        private void HandleLobbyHostRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer) return;
            SendLobbyHostToClient(senderClientId);
        }

        private void HandleLobbyHostChangedMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;

            reader.ReadValueSafe(out ulong hostClientId);
            SetLobbyHost(hostClientId);
        }

        private void SetLobbyHost(ulong clientId)
        {
            if (LobbyHostClientId == clientId) return;

            LobbyHostClientId = clientId;
            LobbyHostChanged?.Invoke(clientId);
        }
    }
}
