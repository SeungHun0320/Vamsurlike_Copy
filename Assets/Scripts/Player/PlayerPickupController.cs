using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Stage;

namespace Vamsurlike.Player
{
    [RequireComponent(typeof(PlayerNetworkStats))]
    public class PlayerPickupController : NetworkBehaviour
    {
        [SerializeField] private float pickupRadius = 2f;

        private float checkTimer;
        private const float CheckInterval = 0.1f;

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

            List<ulong> nearby = XPOrbManager.Instance.GetNearbyOrbIds(transform.position, pickupRadius);
            foreach (ulong id in nearby)
                RequestPickupServerRpc(id);
        }

        [ServerRpc]
        private void RequestPickupServerRpc(ulong orbId)
        {
            XPOrbManager.Instance?.TryPickup(orbId, OwnerClientId);
        }
    }
}
