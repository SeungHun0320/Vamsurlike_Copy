using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Vamsurlike.Network
{
    internal sealed class NetworkSessionService : INetworkSessionService
    {
        // 서버에서 clientId → 닉네임 임시 저장 (스폰 시 PlayerMatchStats에 주입)
        internal static readonly Dictionary<ulong, string> PendingPlayerNames = new();

        private readonly NetworkManager networkManager;
        private readonly UnityTransport transport;

        public string CurrentIp { get; private set; }
        public ushort CurrentPort { get; private set; }

        public NetworkSessionService(
            NetworkManager networkManager,
            UnityTransport transport,
            string defaultIp,
            ushort defaultPort)
        {
            this.networkManager = networkManager;
            this.transport = transport;
            CurrentIp = defaultIp;
            CurrentPort = defaultPort;
        }

        public void ConfigureConnectionApproval()
        {
            if (networkManager == null) return;

            networkManager.NetworkConfig.ConnectionApproval = true;
            if (networkManager.ConnectionApprovalCallback == null)
            {
                networkManager.ConnectionApprovalCallback = HandleConnectionApproval;
                return;
            }

            if (networkManager.ConnectionApprovalCallback != HandleConnectionApproval)
                Debug.LogWarning($"[{nameof(NetworkSessionService)}] 다른 ConnectionApprovalCallback이 이미 등록되어 있습니다.");
        }

        public bool StartClient(string ip, ushort port, string nickname = "")
        {
            if (!CanStart(nameof(StartClient)) || !TrySetTransport(ip, port)) return false;

            ConfigureConnectionApproval();
            SetVersionPayload(nickname);
            bool started = networkManager.StartClient();
            Debug.Log($"[{nameof(NetworkSessionService)}] Client 시작 - {ip}:{port} (ok={started})");
            return started;
        }

        public bool StartRelayClient()
        {
            if (!CanStart(nameof(StartRelayClient))) return false;

            ConfigureConnectionApproval();
            SetVersionPayload();
            bool started = networkManager.StartClient();
            Debug.Log($"[{nameof(NetworkSessionService)}] Relay Client 시작 (ok={started})");
            return started;
        }

        public bool StartServer(string ip, ushort port)
        {
            if (!CanStart(nameof(StartServer)) || !TrySetTransport(ip, port)) return false;

            ConfigureConnectionApproval();
            bool started = networkManager.StartServer();
            Debug.Log($"[{nameof(NetworkSessionService)}] Server 시작 - {ip}:{port} (ok={started})");
            return started;
        }

        public void Disconnect()
        {
            if (networkManager == null || !networkManager.IsListening) return;

            networkManager.Shutdown();
            Debug.Log($"[{nameof(NetworkSessionService)}] 연결 종료.");
        }

        public void Dispose()
        {
            if (networkManager != null && networkManager.ConnectionApprovalCallback == HandleConnectionApproval)
                networkManager.ConnectionApprovalCallback = null;
        }

        private bool CanStart(string caller)
        {
            if (networkManager == null)
            {
                Debug.LogError($"[{nameof(NetworkSessionService)}] {caller}: NetworkManager가 없습니다.");
                return false;
            }

            if (networkManager.IsListening)
            {
                Debug.LogWarning($"[{nameof(NetworkSessionService)}] {caller}: 이미 실행 중입니다.");
                return false;
            }

            return true;
        }

        private bool TrySetTransport(string ip, ushort port)
        {
            if (transport == null)
            {
                Debug.LogError($"[{nameof(NetworkSessionService)}] UnityTransport가 없습니다.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(ip) || port == 0)
            {
                Debug.LogWarning($"[{nameof(NetworkSessionService)}] 유효하지 않은 접속 주소입니다: {ip}:{port}");
                return false;
            }

            transport.SetConnectionData(ip, port);
            CurrentIp = ip;
            CurrentPort = port;
            return true;
        }

        private void SetVersionPayload(string nickname = "")
        {
            byte[] hashBytes = System.BitConverter.GetBytes(CatalogVersionUtility.GetHash());
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(
                string.IsNullOrWhiteSpace(nickname) ? "" : nickname.Trim());
            var payload = new byte[hashBytes.Length + nameBytes.Length];
            hashBytes.CopyTo(payload, 0);
            nameBytes.CopyTo(payload, hashBytes.Length);
            networkManager.NetworkConfig.ConnectionData = payload;
        }

        private static void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            if (!ValidatePayload(request.Payload, out string reason, out string nickname))
            {
                response.Approved = false;
                response.Reason   = reason;
                response.Pending  = false;
                Debug.LogWarning($"[{nameof(NetworkSessionService)}] 접속 거부 (clientId={request.ClientNetworkId}): {reason}");
                return;
            }

            PendingPlayerNames[request.ClientNetworkId] = nickname;
            response.Approved           = true;
            response.CreatePlayerObject = false;
            response.Pending            = false;
        }

        private static bool ValidatePayload(byte[] payload, out string reason, out string nickname)
        {
            nickname = "Player";
            if (payload == null || payload.Length < sizeof(int))
            {
                reason = "연결 데이터가 없습니다.";
                return false;
            }

            int clientHash = System.BitConverter.ToInt32(payload, 0);
            int serverHash = CatalogVersionUtility.GetHash();

            if (clientHash != serverHash)
            {
                reason = "데이터 버전이 서버와 다릅니다. 클라이언트를 업데이트하세요.";
                return false;
            }

            int nameStart = sizeof(int);
            if (payload.Length > nameStart)
            {
                string name = System.Text.Encoding.UTF8.GetString(payload, nameStart, payload.Length - nameStart).Trim();
                if (!string.IsNullOrEmpty(name))
                    nickname = name;
            }

            reason = null;
            return true;
        }
    }
}
