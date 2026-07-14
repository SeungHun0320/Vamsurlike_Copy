using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using UnityEngine;

namespace Vamsurlike.Network
{
    internal sealed class NetworkSessionService : INetworkSessionService
    {
        // 서버에서 clientId → 닉네임 임시 저장 (스폰 시 PlayerMatchStats에 주입)
        internal static readonly Dictionary<ulong, string> PendingPlayerNames = new();

        // 서버에서 clientId → UGS PlayerId 저장 (PlayerProgressionService가 저장 파일 식별에 사용).
        // 닉네임과 달리 스폰 시 소비되어 사라지지 않고 접속 종료까지 유지된다.
        internal static readonly Dictionary<ulong, string> PendingPlayerIds = new();

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
            // 서버는 이 AccessToken을 자기신고 값으로 신뢰하지 않고 JWKS 서명 검증을 거친다 (UgsJwtVerifier).
            string accessToken = AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn
                ? AuthenticationService.Instance.AccessToken
                : "";
            SetVersionPayload(nickname, accessToken);
            bool started = networkManager.StartClient();
            Debug.Log($"[{nameof(NetworkSessionService)}] Client 시작 - {ip}:{port} (ok={started})");
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

        // 페이로드 형식: [4바이트 카탈로그 해시][2바이트 닉네임 길이][닉네임 UTF8][AccessToken UTF8(나머지 전부)]
        private void SetVersionPayload(string nickname, string accessToken)
        {
            byte[] hashBytes = System.BitConverter.GetBytes(CatalogVersionUtility.GetHash());
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(
                string.IsNullOrWhiteSpace(nickname) ? "" : nickname.Trim());
            byte[] nameLenBytes = System.BitConverter.GetBytes((ushort)nameBytes.Length);
            byte[] idBytes = System.Text.Encoding.UTF8.GetBytes(accessToken ?? "");

            var payload = new byte[hashBytes.Length + nameLenBytes.Length + nameBytes.Length + idBytes.Length];
            int offset = 0;
            hashBytes.CopyTo(payload, offset);     offset += hashBytes.Length;
            nameLenBytes.CopyTo(payload, offset);  offset += nameLenBytes.Length;
            nameBytes.CopyTo(payload, offset);     offset += nameBytes.Length;
            idBytes.CopyTo(payload, offset);

            networkManager.NetworkConfig.ConnectionData = payload;
        }

        // NGO의 비동기 승인 패턴: response.Pending = true로 완료를 미루고, JWKS 서명 검증(네트워크 호출)이
        // 끝난 뒤 최종 Approved/Reason/Pending을 채운다.
        private static async void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            if (!ValidatePayload(request.Payload, out string reason, out string nickname, out string accessToken))
            {
                response.Approved = false;
                response.Reason   = reason;
                response.Pending  = false;
                Debug.LogWarning($"[{nameof(NetworkSessionService)}] 접속 거부 (clientId={request.ClientNetworkId}): {reason}");
                return;
            }

            response.Pending = true;
            JwtVerifyResult verifyResult = await UgsJwtVerifier.VerifyAsync(accessToken);
            if (!verifyResult.Success)
            {
                response.Approved = false;
                response.Reason   = verifyResult.FailureReason;
                response.Pending  = false;
                ServerConsoleLogger.Log($"[검증] 접속 거부 (clientId={request.ClientNetworkId}): {verifyResult.FailureReason}");
                return;
            }

            if (IsNicknameTaken(nickname))
            {
                response.Approved = false;
                response.Reason   = "이미 사용 중인 닉네임입니다. 다른 이름을 입력하세요.";
                response.Pending  = false;
                Debug.LogWarning($"[{nameof(NetworkSessionService)}] 접속 거부 (clientId={request.ClientNetworkId}): 닉네임 중복 '{nickname}'");
                return;
            }

            PendingPlayerNames[request.ClientNetworkId] = nickname;
            PendingPlayerIds[request.ClientNetworkId] = verifyResult.PlayerId;
            response.Approved           = true;
            response.CreatePlayerObject = false;
            response.Pending            = false;
        }

        // 현재 로비에 접속 중인 다른 플레이어와 닉네임이 겹치는지 확인 (대소문자 무시).
        private static bool IsNicknameTaken(string nickname)
        {
            foreach (var state in LobbyPlayerState.All.Values)
            {
                if (state == null) continue;
                if (string.Equals(state.DisplayName.Value.ToString().Trim(), nickname, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool ValidatePayload(byte[] payload, out string reason, out string nickname, out string accessToken)
        {
            nickname = "Player";
            accessToken = "";
            if (payload == null || payload.Length < sizeof(int) + sizeof(ushort))
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

            int offset = sizeof(int);
            ushort nameLen = System.BitConverter.ToUInt16(payload, offset);
            offset += sizeof(ushort);

            if (offset + nameLen > payload.Length)
            {
                reason = "연결 데이터 형식이 올바르지 않습니다.";
                return false;
            }

            if (nameLen > 0)
            {
                string name = System.Text.Encoding.UTF8.GetString(payload, offset, nameLen).Trim();
                if (!string.IsNullOrEmpty(name))
                    nickname = name;
            }
            offset += nameLen;

            if (payload.Length > offset)
                accessToken = System.Text.Encoding.UTF8.GetString(payload, offset, payload.Length - offset).Trim();

            reason = null;
            return true;
        }
    }
}
