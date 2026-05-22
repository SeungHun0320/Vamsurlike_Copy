using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Multiplayer;

namespace Vamsurlike.Network
{
    // com.unity.services.multiplayer 1.0.0 기반 세션(Relay 통합) 관리
    public class RelayManager : MonoBehaviour
    {
        public static RelayManager Instance { get; private set; }

        public string SessionCode => currentSession?.Code;
        public bool IsInSession => currentSession != null;

        private ISession currentSession;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (currentSession == null) return;
            var s = currentSession;
            currentSession = null;
            _ = BestEffortLeaveAsync(s);
        }

        private static async System.Threading.Tasks.Task BestEffortLeaveAsync(ISession session)
        {
            try { await session.LeaveAsync(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[RelayManager] OnDestroy leave 실패 (무시): {e.Message}");
            }
        }

        // 세션(Relay 포함) 생성 후 join code 반환. 실패 시 null.
        public async Task<string> CreateSessionAsync(int maxPlayers = 4)
        {
            try
            {
                var options = new SessionOptions { MaxPlayers = maxPlayers }
                    .WithRelayNetwork();
                currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                Debug.Log($"[{nameof(RelayManager)}] 세션 생성 완료. Code: {currentSession.Code}");
                return currentSession.Code;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(RelayManager)}] 세션 생성 실패: {e.Message}");
                return null;
            }
        }

        // 세션 코드로 참여. 성공 시 true.
        public async Task<bool> JoinSessionAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                Debug.LogWarning($"[{nameof(RelayManager)}] 세션 코드가 비어 있습니다.");
                return false;
            }
            try
            {
                currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                Debug.Log($"[{nameof(RelayManager)}] 세션 참여 성공. Code: {code}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(RelayManager)}] 세션 참여 실패: {e.Message}");
                return false;
            }
        }

        public async Task LeaveSessionAsync()
        {
            if (currentSession == null) return;
            try
            {
                await currentSession.LeaveAsync();
                Debug.Log($"[{nameof(RelayManager)}] 세션 종료.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{nameof(RelayManager)}] 세션 종료 중 오류: {e.Message}");
            }
            finally
            {
                currentSession = null;
            }
        }

    }
}
