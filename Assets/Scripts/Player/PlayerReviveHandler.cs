using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Player
{
    // 서버: 다운 타이머 관리 + 부활 처리
    // 클라이언트: 진행도/완료 이벤트 수신
    public class PlayerReviveHandler : NetworkBehaviour
    {
        [SerializeField] private float downedDuration = 30f;
        [SerializeField] private float reviveDuration = 3f;
        [SerializeField] private float reviveHPRatio  = 0.3f;
        [SerializeField] private float reviveRadius   = 2.5f;

        // 전체 클라이언트가 읽어 HUD 표시에 활용
        public NetworkVariable<float> DownedTimeRemaining { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 클라이언트 이벤트 (progress: 0~1 진행도, -1 취소)
        public static event Action<float> OnReviveProgressUpdated;
        public static event Action        OnRevived;

        // 클라이언트에서 거리 탐색에 사용하는 전역 인스턴스 목록
        public static readonly List<PlayerReviveHandler> All = new();

        private PlayerNetworkStats stats;
        private Coroutine          downedCoroutine;
        private Coroutine          reviveCoroutine;
        private ulong              reviverClientId = NoReviver;

        private const ulong NoReviver = ulong.MaxValue;

        private void Awake()
        {
            stats = GetComponent<PlayerNetworkStats>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
        }

        // PlayerNetworkStats.TakeDamage에서 HP=0 될 때 호출 (서버 전용)
        public void BeginDowned()
        {
            if (!IsServer || stats == null) return;
            if (stats.IsDowned.Value) return;

            stats.IsDowned.Value      = true;
            DownedTimeRemaining.Value = downedDuration;
            reviverClientId           = NoReviver;

            if (downedCoroutine != null) StopCoroutine(downedCoroutine);
            downedCoroutine = StartCoroutine(DownedTimerCoroutine());

        }

        private IEnumerator DownedTimerCoroutine()
        {
            while (DownedTimeRemaining.Value > 0f)
            {
                yield return new WaitForSeconds(1f);
                DownedTimeRemaining.Value = Mathf.Max(0f, DownedTimeRemaining.Value - 1f);
            }

            if (reviveCoroutine != null) { StopCoroutine(reviveCoroutine); reviveCoroutine = null; }
            stats.IsDowned.Value = false;
            reviverClientId      = NoReviver;
        }

        // 구조자가 E를 누를 때 호출
        [ServerRpc(RequireOwnership = false)]
        public void BeginReviveServerRpc(ServerRpcParams rpc = default)
        {
            ulong senderClientId = rpc.Receive.SenderClientId;

            if (senderClientId == OwnerClientId)
            {
                Debug.LogWarning($"[BeginReviveServerRpc] FAIL — 자기 자신 부활 불가. sender={senderClientId}");
                return;
            }
            if (stats == null || !stats.IsDowned.Value)
            {
                Debug.LogWarning($"[BeginReviveServerRpc] FAIL — IsDowned=false. target={OwnerClientId}");
                return;
            }
            if (!IsRescuerInRange(senderClientId))
            {
                Debug.LogWarning($"[BeginReviveServerRpc] FAIL — 거리 초과. sender={senderClientId}, radius={reviveRadius}");
                return;
            }

            if (reviveCoroutine != null) StopCoroutine(reviveCoroutine);
            reviverClientId = senderClientId;
            reviveCoroutine = StartCoroutine(ReviveProgressCoroutine(senderClientId));
            Debug.Log($"[BeginReviveServerRpc] OK — 부활 시작. target={OwnerClientId}, rescuer={senderClientId}");
        }

        // 구조자가 E를 뗄 때 호출
        [ServerRpc(RequireOwnership = false)]
        public void CancelReviveServerRpc(ServerRpcParams rpc = default)
        {
            ulong senderClientId = rpc.Receive.SenderClientId;
            if (senderClientId != reviverClientId)
            {
                Debug.LogWarning($"[CancelReviveServerRpc] FAIL — 구조자 불일치. sender={senderClientId}, expected={reviverClientId}");
                return;
            }
            Debug.Log($"[CancelReviveServerRpc] OK — 취소. rescuer={senderClientId}");
            CancelRevive();
        }

        private IEnumerator ReviveProgressCoroutine(ulong rescuerClientId)
        {
            float elapsed       = 0f;
            float rangeCheckAcc = 0f;

            while (elapsed < reviveDuration)
            {
                elapsed       += Time.deltaTime;
                rangeCheckAcc += Time.deltaTime;

                // 매 프레임 체크시 위치 오차로 즉시 취소될 수 있어서 0.5초 주기로 검사
                if (rangeCheckAcc >= 0.5f)
                {
                    rangeCheckAcc = 0f;
                    if (!IsRescuerInRange(rescuerClientId))
                    {
                        CancelRevive();
                        yield break;
                    }
                }

                SendReviveProgressClientRpc(
                    Mathf.Clamp01(elapsed / reviveDuration),
                    new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { rescuerClientId } }
                    });

                yield return null;
            }

            CompleteRevive();
        }

        private void CompleteRevive()
        {
            if (downedCoroutine != null) { StopCoroutine(downedCoroutine); downedCoroutine = null; }
            if (reviveCoroutine != null) { StopCoroutine(reviveCoroutine); reviveCoroutine = null; }

            stats.HP.Value            = Mathf.Max(1f, stats.MaxHP.Value * reviveHPRatio);
            DownedTimeRemaining.Value = 0f;
            stats.IsDowned.Value      = false;
            reviverClientId           = NoReviver;

            NotifyRevivedClientRpc();
        }

        private void CancelRevive()
        {
            if (reviveCoroutine != null) { StopCoroutine(reviveCoroutine); reviveCoroutine = null; }
            reviverClientId = NoReviver;
            CancelReviveProgressClientRpc();
        }

        private bool IsRescuerInRange(ulong rescuerClientId)
        {
            if (NetworkManager.Singleton == null) return false;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(rescuerClientId, out var client)) return false;
            if (client.PlayerObject == null) return false;
            return Vector3.Distance(transform.position, client.PlayerObject.transform.position) <= reviveRadius;
        }

        [ClientRpc]
        private void SendReviveProgressClientRpc(float progress, ClientRpcParams rpc = default)
            => OnReviveProgressUpdated?.Invoke(progress);

        [ClientRpc]
        private void CancelReviveProgressClientRpc()
            => OnReviveProgressUpdated?.Invoke(-1f);

        [ClientRpc]
        private void NotifyRevivedClientRpc()
            => OnRevived?.Invoke();
    }
}
