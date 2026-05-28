using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Stage;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(PlayerNetworkStats))]
    public class PlayerPickupController : NetworkBehaviour
    {
        private PlayerNetworkStats stats;
        private float checkTimer;
        private const float CheckInterval = 0.1f;
        private readonly List<ulong> nearbyOrbs = new();

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
        }

        private void CheckXPPickups()
        {
            if (XPOrbManager.Instance == null) return;

            float radius = stats != null && stats.PickupRadius.Value > 0f
                ? stats.PickupRadius.Value : 2f;
            XPOrbManager.Instance.FillNearbyOrbIds(transform.position, radius, nearbyOrbs);
            foreach (ulong id in nearbyOrbs)
                RequestPickupServerRpc(id);
        }

        [ServerRpc]
        private void RequestPickupServerRpc(ulong orbId)
        {
            XPOrbManager.Instance?.TryPickup(orbId, OwnerClientId);
        }
    }
}
