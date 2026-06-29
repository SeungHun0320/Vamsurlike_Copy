using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Stage;
using Vamsurlike.UI.Events;

namespace Vamsurlike.Player
{
    // 2단계 다운 시스템
    //   1단계 (Downed)      : 동료가 부활 가능. DownedTimeRemaining 카운트다운.
    //                         살아있는 플레이어가 reviveRadius 안에 들어오면 자동으로 부활 채널 시작.
    //   2단계 (DeadWaiting) : 부활 불가. DeadWaitRemaining 카운트다운 후 자동 부활.
    //                         사망 횟수(deathCount)마다 2단계 대기 시간이 deathPenaltyRatio 비율로 증가.
    // GameOver = 전원 동시 다운(1단계 또는 2단계 포함).
    public class PlayerReviveHandler : NetworkBehaviour
    {
        [SerializeField] private float downedDuration       = 30f;  // 1단계: 동료 부활 가능 시간
        [SerializeField] private float baseDeadWaitDuration = 10f;  // 2단계: 첫 번째 자동 부활 대기 시간
        [SerializeField] private float deathPenaltyRatio    = 0.5f; // 사망마다 2단계 대기 시간 증가 비율
        [SerializeField] private float reviveDuration       = 2f;   // 동료 부활에 걸리는 시간
        [SerializeField] private float reviveHPRatio        = 0.3f;
        [SerializeField] private float reviveRadius         = 2.5f;

        // 1단계 타이머 (동료 부활 가능 창)
        public NetworkVariable<float> DownedTimeRemaining { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 2단계 타이머 (자동 부활 대기, 부활 불가)
        public NetworkVariable<float> DeadWaitRemaining { get; } = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 클라이언트 이벤트 (progress: 0~1 진행도, -1 취소)
        public static event Action<float> OnReviveProgressUpdated;
        public static event Action        OnRevived;

        // ReviveRangeVisualizer에서 반경 참조용
        public static readonly List<PlayerReviveHandler> All = new();
        public float ReviveRadius => reviveRadius;

        private PlayerNetworkStats stats;
        private Coroutine          downedCoroutine;
        private Coroutine          deadWaitCoroutine;
        private Coroutine          reviveCoroutine;
        private ulong              reviverClientId = NoReviver;
        private int                deathCount      = 0; // 서버 전용

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
            StopAllReviveActivity();
        }

        // GameOver 시 서버에서 호출 — 진행 중인 모든 부활 타이머/코루틴 즉시 중단
        public void StopAllReviveActivity()
        {
            if (downedCoroutine   != null) { StopCoroutine(downedCoroutine);   downedCoroutine   = null; }
            if (deadWaitCoroutine != null) { StopCoroutine(deadWaitCoroutine); deadWaitCoroutine = null; }
            if (reviveCoroutine   != null) { StopCoroutine(reviveCoroutine);   reviveCoroutine   = null; }
            reviverClientId = NoReviver;
        }

        // ─── 자동 근접 감지 (서버 전용) ────────────────────────────────────
        // 1단계 중 범위 안에 살아있는 플레이어가 있으면 자동으로 부활 시작.
        // ReviveProgressCoroutine이 0.5초마다 범위 재확인해 이탈 시 자동 취소.
        private void Update()
        {
            if (!IsServer) return;
            if (stats == null || !stats.IsDowned.Value) return;
            if (reviveCoroutine != null) return; // 이미 진행 중

            ulong rescuerId = FindNearbyRescuer();
            if (rescuerId == NoReviver) return;

            reviverClientId = rescuerId;
            reviveCoroutine = StartCoroutine(ReviveProgressCoroutine(rescuerId));
        }

        private ulong FindNearbyRescuer()
        {
            if (NetworkManager.Singleton == null) return NoReviver;

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == OwnerClientId) continue;
                if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) continue;
                if (client.PlayerObject == null) continue;

                var rescuerStats = client.PlayerObject.GetComponent<PlayerNetworkStats>();
                if (rescuerStats == null || !rescuerStats.CanAct) continue;

                if (Vector3.Distance(transform.position, client.PlayerObject.transform.position) <= reviveRadius)
                    return clientId;
            }
            return NoReviver;
        }

        // ─── 1단계 진입 ────────────────────────────────────────────────────
        // PlayerNetworkStats.TakeDamage에서 HP=0 될 때 호출 (서버 전용)
        public void BeginDowned()
        {
            if (!IsServer || stats == null) return;
            if (stats.IsDowned.Value || stats.IsDeadWaiting.Value) return;

            stats.IsDowned.Value      = true;
            DownedTimeRemaining.Value = downedDuration;
            reviverClientId           = NoReviver;

            if (downedCoroutine != null) StopCoroutine(downedCoroutine);
            downedCoroutine = StartCoroutine(DownedTimerCoroutine());

            // 전원 동시 다운 여부 확인 — 모두 CanAct=false이면 GameOver
            StageRuntime.Instance?.CheckGameOver();
        }

        // 1단계 타이머 만료 → 2단계(DeadWaiting)로 전환
        private IEnumerator DownedTimerCoroutine()
        {
            while (DownedTimeRemaining.Value > 0f)
            {
                yield return new WaitForSeconds(1f);
                DownedTimeRemaining.Value = Mathf.Max(0f, DownedTimeRemaining.Value - 1f);
            }

            if (reviveCoroutine != null) { StopCoroutine(reviveCoroutine); reviveCoroutine = null; }
            downedCoroutine           = null;
            reviverClientId           = NoReviver;
            DownedTimeRemaining.Value = 0f;
            stats.IsDowned.Value      = false;

            BeginDeadWait();
        }

        // ─── 2단계 진입 ────────────────────────────────────────────────────
        private void BeginDeadWait()
        {
            deathCount++;
            float waitDuration = baseDeadWaitDuration
                * Mathf.Pow(1f + deathPenaltyRatio, deathCount - 1);

            stats.IsDeadWaiting.Value = true;
            DeadWaitRemaining.Value   = waitDuration;

            if (deadWaitCoroutine != null) StopCoroutine(deadWaitCoroutine);
            deadWaitCoroutine = StartCoroutine(DeadWaitCoroutine());
        }

        // 2단계 타이머 만료 → 자동 부활
        private IEnumerator DeadWaitCoroutine()
        {
            while (DeadWaitRemaining.Value > 0f)
            {
                yield return new WaitForSeconds(1f);
                DeadWaitRemaining.Value = Mathf.Max(0f, DeadWaitRemaining.Value - 1f);
            }

            deadWaitCoroutine         = null;
            DeadWaitRemaining.Value   = 0f;
            stats.HP.Value            = Mathf.Max(1f, stats.MaxHP.Value * reviveHPRatio);
            stats.IsDeadWaiting.Value = false;

            NotifyRevivedClientRpc();
        }

        // ─── 동료 부활 처리 ───────────────────────────────────────────────
        private IEnumerator ReviveProgressCoroutine(ulong rescuerClientId)
        {
            float elapsed       = 0f;
            float rangeCheckAcc = 0f;

            while (elapsed < reviveDuration)
            {
                elapsed       += Time.deltaTime;
                rangeCheckAcc += Time.deltaTime;

                // 0.5초 주기로 범위 이탈 여부 확인 (매 프레임 체크시 위치 오차로 즉시 취소될 수 있음)
                if (rangeCheckAcc >= 0.5f)
                {
                    rangeCheckAcc = 0f;
                    if (!IsRescuerInRange(rescuerClientId))
                    {
                        CancelRevive(rescuerClientId);
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

            CompleteRevive(rescuerClientId);
        }

        // 동료에 의한 부활 — deathCount 증가 없음
        private void CompleteRevive(ulong rescuerClientId)
        {
            if (downedCoroutine != null) { StopCoroutine(downedCoroutine); downedCoroutine = null; }
            if (reviveCoroutine != null) { StopCoroutine(reviveCoroutine); reviveCoroutine = null; }

            stats.HP.Value            = Mathf.Max(1f, stats.MaxHP.Value * reviveHPRatio);
            DownedTimeRemaining.Value = 0f;
            stats.IsDowned.Value      = false;
            reviverClientId           = NoReviver;

            NotifyRevivedClientRpc();
        }

        private void CancelRevive(ulong rescuerClientId)
        {
            if (reviveCoroutine != null) { StopCoroutine(reviveCoroutine); reviveCoroutine = null; }
            reviverClientId = NoReviver;
            CancelReviveProgressClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { rescuerClientId } }
            });
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
        {
            OnReviveProgressUpdated?.Invoke(progress); // 임시 경유지 (Phase 8 마이그레이션 완료 후 제거)
            UIEventHub.Instance?.Player.PublishReviveProgress(new ReviveProgressPayload(progress));
        }

        [ClientRpc]
        private void CancelReviveProgressClientRpc(ClientRpcParams rpc = default)
        {
            OnReviveProgressUpdated?.Invoke(-1f); // 임시 경유지 (Phase 8 마이그레이션 완료 후 제거)
            UIEventHub.Instance?.Player.PublishReviveProgress(new ReviveProgressPayload(-1f));
        }

        [ClientRpc]
        private void NotifyRevivedClientRpc()
        {
            OnRevived?.Invoke(); // 임시 경유지 (Phase 8 마이그레이션 완료 후 제거)
            UIEventHub.Instance?.Player.PublishPlayerRevived();
        }
    }
}
