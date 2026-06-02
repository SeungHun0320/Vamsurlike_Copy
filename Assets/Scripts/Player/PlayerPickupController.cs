using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Items;
using Vamsurlike.Stage;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(PlayerNetworkStats))]
    public class PlayerPickupController : NetworkBehaviour
    {
        private PlayerNetworkStats stats;
        private float checkTimer;
        private const float CheckInterval  = 0.1f;
        private const float ItemPickupRadius = 1.5f;

        private readonly List<ulong>              nearbyOrbs  = new();
        private readonly List<NetworkedItemPickup> nearbyItems = new();

        private void Awake()
        {
            stats = GetComponent<PlayerNetworkStats>();
        }

        private void Update()
        {
            if (!IsOwner) return;

            checkTimer -= Time.deltaTime;
            if (checkTimer > 0f) return;
            checkTimer = CheckInterval;

            CheckXPPickups();
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

        private void CheckItemPickups()
        {
            // 클라이언트 로컬 OverlapSphere — 서버에서 거리 재검증함
            var cols = Physics.OverlapSphere(transform.position, ItemPickupRadius);
            foreach (var col in cols)
            {
                if (!col.TryGetComponent<NetworkedItemPickup>(out var pickup)) continue;
                if (!pickup.IsSpawned) continue;
                pickup.RequestPickupRpc();
            }
        }

        [ServerRpc]
        private void RequestXPPickupServerRpc(ulong orbId)
        {
            XPOrbManager.Instance?.TryPickup(orbId, OwnerClientId);
        }
    }
}
