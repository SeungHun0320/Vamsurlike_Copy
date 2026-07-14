using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Core;
using Vamsurlike.Data.Runtime;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Network
{
    // 골드/영구 업그레이드의 유일한 권위는 서버다. 클라이언트의 MetaProgressionState는 이 서비스가
    // 보내주는 동기화 메시지를 받아 표시만 하는 미러일 뿐, 스스로 구매를 확정하지 않는다.
    internal sealed class PlayerProgressionService : IPlayerProgressionService
    {
        private const string PurchaseRequestMessage      = "Vamsurlike.PurchaseRequest";
        private const string ProgressionSyncMessage      = "Vamsurlike.ProgressionSync";
        private const string DebugGrantGoldMessage       = "Vamsurlike.DebugGrantGoldRequest";
        private const string DebugResetUpgradesMessage   = "Vamsurlike.DebugResetUpgradesRequest";
        private const string DebugResetGoldMessage       = "Vamsurlike.DebugResetGoldRequest";

        private readonly NetworkManager networkManager;
        private bool isMessageHandlerRegistered;

        // 서버 전용 — clientId별 인메모리 진행도(권위 본체).
        private readonly Dictionary<ulong, MetaProgressionState> serverStates = new();
        private readonly Dictionary<ulong, string> serverPlayerIds = new();

        public PlayerProgressionService(NetworkManager networkManager)
        {
            this.networkManager = networkManager;
        }

        public void RegisterMessageHandler()
        {
            if (isMessageHandlerRegistered || networkManager?.CustomMessagingManager == null) return;

            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                PurchaseRequestMessage, HandlePurchaseRequestMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                ProgressionSyncMessage, HandleProgressionSyncMessage);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                DebugGrantGoldMessage, HandleDebugGrantGoldMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                DebugResetUpgradesMessage, HandleDebugResetUpgradesMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                DebugResetGoldMessage, HandleDebugResetGoldMessage);
#endif
            isMessageHandlerRegistered = true;
        }

        public void UnregisterMessageHandler()
        {
            if (!isMessageHandlerRegistered || networkManager?.CustomMessagingManager == null) return;

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(PurchaseRequestMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ProgressionSyncMessage);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(DebugGrantGoldMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(DebugResetUpgradesMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(DebugResetGoldMessage);
#endif
            isMessageHandlerRegistered = false;
        }

        // 서버: 클라이언트 접속 시 저장 파일을 로드하고, 그 클라이언트에게만 동기화를 보낸다.
        public void HandleClientConnected(ulong clientId)
        {
            if (networkManager == null || !networkManager.IsServer) return;

            // 서버는 상점 UI를 열 일이 없어 DataManager.Initialize()를 아무도 호출해주지 않는다 —
            // 구매 검증(GetPermanentUpgrade)이 서버에서 돌기 전에 반드시 초기화해야 한다. 멱등 호출.
            if (!DataManager.IsInitialized)
                DataManager.Initialize();

            string playerId = NetworkSessionService.PendingPlayerIds.TryGetValue(clientId, out string id) ? id : "";
            serverPlayerIds[clientId] = playerId;

            var state = new MetaProgressionState();
            if (!string.IsNullOrEmpty(playerId))
            {
                PlayerSaveData saved = ServerSaveFileStore.LoadOrCreate(playerId);
                state.LoadFromSnapshot(saved.Gold, saved.UpgradeLevels);
            }
            else
            {
                ServerConsoleLogger.Log($"[검증] clientId={clientId} PlayerId 없음 — 진행도 저장 안 됨(세션 한정)");
            }

            serverStates[clientId] = state;
            SendSyncToClient(clientId, state);
        }

        public void HandleClientDisconnected(ulong clientId)
        {
            serverStates.Remove(clientId);
            serverPlayerIds.Remove(clientId);
            NetworkSessionService.PendingPlayerIds.Remove(clientId);
        }

        public void CreditGold(ulong clientId, int amount)
        {
            if (networkManager == null || !networkManager.IsServer) return;
            if (amount <= 0) return;
            if (!serverStates.TryGetValue(clientId, out var state)) return;

            state.AddGold(amount);
            PersistAndSync(clientId, state);
        }

        public void RequestPurchase(PermanentUpgradeType type)
        {
            if (networkManager == null || !networkManager.IsConnectedClient || networkManager.CustomMessagingManager == null)
                return;

            using FastBufferWriter writer = new(sizeof(byte), Allocator.Temp);
            writer.WriteValueSafe((byte)type);
            networkManager.CustomMessagingManager.SendNamedMessage(
                PurchaseRequestMessage, NetworkManager.ServerClientId, writer);
        }

        public void RequestDebugGrantGold(int amount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (networkManager == null || !networkManager.IsConnectedClient || networkManager.CustomMessagingManager == null)
                return;

            using FastBufferWriter writer = new(sizeof(int), Allocator.Temp);
            writer.WriteValueSafe(amount);
            networkManager.CustomMessagingManager.SendNamedMessage(
                DebugGrantGoldMessage, NetworkManager.ServerClientId, writer);
#endif
        }

        public void RequestDebugResetUpgrades()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SendEmptyDebugMessage(DebugResetUpgradesMessage);
#endif
        }

        public void RequestDebugResetGold()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SendEmptyDebugMessage(DebugResetGoldMessage);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void SendEmptyDebugMessage(string messageName)
        {
            if (networkManager == null || !networkManager.IsConnectedClient || networkManager.CustomMessagingManager == null)
                return;

            using FastBufferWriter writer = new(sizeof(byte), Allocator.Temp);
            writer.WriteValueSafe((byte)0);
            networkManager.CustomMessagingManager.SendNamedMessage(
                messageName, NetworkManager.ServerClientId, writer);
        }
#endif

        private void HandlePurchaseRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer) return;
            if (!serverStates.TryGetValue(senderClientId, out var state)) return;

            reader.ReadValueSafe(out byte typeByte);
            var type = (PermanentUpgradeType)typeByte;

            bool purchased = state.TryPurchase(type);
            ServerConsoleLogger.Log($"[검증] 구매 요청 — sender={senderClientId}, type={type} → {(purchased ? "승인" : "거부")}");
            if (purchased)
                PersistAndSync(senderClientId, state);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void HandleDebugGrantGoldMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer) return;
            if (!serverStates.TryGetValue(senderClientId, out var state)) return;

            reader.ReadValueSafe(out int amount);
            if (amount <= 0) return;

            state.AddGold(amount);
            ServerConsoleLogger.Log($"[디버그] clientId={senderClientId} 골드 +{amount} 지급");
            PersistAndSync(senderClientId, state);
        }

        // 업그레이드 레벨만 전부 0으로 — 골드는 그대로 둔다.
        private void HandleDebugResetUpgradesMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer) return;
            if (!serverStates.TryGetValue(senderClientId, out var state)) return;

            state.LoadFromSnapshot(state.Gold, new int[13]);
            ServerConsoleLogger.Log($"[디버그] clientId={senderClientId} 영구 업그레이드 초기화");
            PersistAndSync(senderClientId, state);
        }

        // 골드만 0으로 — 업그레이드 레벨은 그대로 둔다.
        private void HandleDebugResetGoldMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer) return;
            if (!serverStates.TryGetValue(senderClientId, out var state)) return;

            state.LoadFromSnapshot(0, state.BuildLevelSnapshot());
            ServerConsoleLogger.Log($"[디버그] clientId={senderClientId} 골드 초기화");
            PersistAndSync(senderClientId, state);
        }
#endif

        private void PersistAndSync(ulong clientId, MetaProgressionState state)
        {
            if (serverPlayerIds.TryGetValue(clientId, out string playerId) && !string.IsNullOrEmpty(playerId))
            {
                ServerSaveFileStore.Save(playerId, new PlayerSaveData
                {
                    Gold = state.Gold,
                    UpgradeLevels = state.BuildLevelSnapshot(),
                });
            }
            SendSyncToClient(clientId, state);
        }

        private void SendSyncToClient(ulong clientId, MetaProgressionState state)
        {
            if (networkManager == null || !networkManager.IsServer || networkManager.CustomMessagingManager == null) return;

            int[] levels = state.BuildLevelSnapshot();
            using FastBufferWriter writer = new(sizeof(int) * (2 + levels.Length), Allocator.Temp);
            writer.WriteValueSafe(state.Gold);
            writer.WriteValueSafe(levels.Length);
            foreach (int lvl in levels)
                writer.WriteValueSafe(lvl);

            networkManager.CustomMessagingManager.SendNamedMessage(
                ProgressionSyncMessage, clientId, writer);
        }

        // 클라이언트: 서버 동기화 수신 → 로컬 표시용 미러 갱신.
        private void HandleProgressionSyncMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            if (GameInstance.I == null) return;

            reader.ReadValueSafe(out int gold);
            reader.ReadValueSafe(out int count);
            var levels = new int[count];
            for (int i = 0; i < count; i++)
                reader.ReadValueSafe(out levels[i]);

            GameInstance.I.MetaProgression.LoadFromSnapshot(gold, levels);
        }

        public void Dispose()
        {
            UnregisterMessageHandler();
        }
    }
}
