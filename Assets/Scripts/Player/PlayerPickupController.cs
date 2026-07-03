using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Items;
using Vamsurlike.Network;
using Vamsurlike.Stage;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(PlayerNetworkStats))]
    public class PlayerPickupController : OwnerBehaviour
    {
        private PlayerNetworkStats stats;
        private float checkTimer;
        private const float CheckInterval  = 0.1f;
        private const float ItemPickupRadius = 1.5f;

        private readonly List<ulong>   nearbyOrbs           = new();
        private readonly List<ulong>   nearbyGoldOrbs       = new();
        private readonly HashSet<ulong> pendingPickupRequests = new();

        private void Awake()
        {
            stats = GetComponent<PlayerNetworkStats>();
        }

        private void OnEnable()
        {
            NetworkedItemPickup.OnPickupRejected += HandlePickupRejected;
        }

        private void OnDisable()
        {
            NetworkedItemPickup.OnPickupRejected -= HandlePickupRejected;
        }

        private void HandlePickupRejected(ulong networkObjectId)
        {
            pendingPickupRequests.Remove(networkObjectId);
        }

        private void Update()
        {
            checkTimer -= Time.deltaTime;
            if (checkTimer > 0f) return;
            checkTimer = CheckInterval;

            CheckXPPickups();
            CheckGoldPickups();
            CheckItemPickups();
        }

        private void CheckXPPickups()
        {
            if (XPOrbManager.Instance == null) return;

            float radius = stats != null && stats.PickupRadius.Value > 0f
                ? stats.PickupRadius.Value : 2f;
            XPOrbManager.Instance.FillNearbyOrbIds(transform.position, radius, nearbyOrbs);
            foreach (ulong id in nearbyOrbs)
                RequestXPPickupServerRpc(id);
        }

        private void CheckGoldPickups()
        {
            if (GoldOrbManager.Instance == null) return;

            float radius = stats != null && stats.PickupRadius.Value > 0f
                ? stats.PickupRadius.Value : 2f;
            GoldOrbManager.Instance.FillNearbyOrbIds(transform.position, radius, nearbyGoldOrbs);
            foreach (ulong id in nearbyGoldOrbs)
                RequestGoldPickupServerRpc(id);
        }

        private void CheckItemPickups()
        {
            var cols = Physics.OverlapSphere(transform.position, ItemPickupRadius);

            // 이번 프레임 범위 내 활성 아이템 ID 수집 → 범위 이탈/소멸 항목 정리
            var inRange = new HashSet<ulong>(cols.Length);
            foreach (var col in cols)
                if (col.TryGetComponent<NetworkedItemPickup>(out var p) && p.IsSpawned)
                    inRange.Add(p.NetworkObjectId);
            pendingPickupRequests.IntersectWith(inRange);

            // 이미 요청한 아이템은 건너뜀 — 서버에서 거리 재검증함
            foreach (var col in cols)
            {
                if (!col.TryGetComponent<NetworkedItemPickup>(out var pickup)) continue;
                if (!pickup.IsSpawned) continue;
                if (!pendingPickupRequests.Add(pickup.NetworkObjectId)) continue;
                pickup.RequestPickupRpc();
            }
        }

        [ServerRpc]
        private void RequestXPPickupServerRpc(ulong orbId)
        {
            XPOrbManager.Instance?.TryPickup(orbId, OwnerClientId);
        }

        [ServerRpc]
        private void RequestGoldPickupServerRpc(ulong orbId)
        {
            GoldOrbManager.Instance?.TryPickup(orbId, OwnerClientId);
        }
    }
}
