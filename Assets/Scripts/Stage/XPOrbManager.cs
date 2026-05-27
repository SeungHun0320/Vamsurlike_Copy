using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Player;

namespace Vamsurlike.Stage
{
    // Stage 씬의 NetworkObject로 배치.
    // 서버: activeOrbs 목록 관리 + 픽업 검증
    // 모든 클라이언트: orbVisuals(로컬 비주얼 프록시) 관리
    public class XPOrbManager : NetworkBehaviour
    {
        public static XPOrbManager Instance { get; private set; }

        [SerializeField] private GameObject orbVisualPrefab;

        // 서버 전용
        private readonly Dictionary<ulong, XPOrbEntry> activeOrbs = new();
        private ulong nextOrbId;

        // 클라이언트 전용: id → 비주얼 프록시 오브젝트
        private readonly Dictionary<ulong, GameObject> orbVisuals = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 서버 전용: 적 사망 시 호출
        public void SpawnOrb(Vector3 position, int xp)
        {
            if (!IsServer) return;
            ulong id = nextOrbId++;
            activeOrbs[id] = new XPOrbEntry(id, position, xp);
            SpawnOrbVisualClientRpc(id, position);
        }

        // 서버 전용: PlayerPickupController.RequestPickupServerRpc에서 호출
        public bool TryPickup(ulong orbId, ulong clientId)
        {
            if (!IsServer) return false;
            if (!activeOrbs.TryGetValue(orbId, out var orb)) return false;

            activeOrbs.Remove(orbId);

            if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var client))
                client.PlayerObject?.GetComponent<PlayerNetworkStats>()?.AddXP(orb.Xp);

            DestroyOrbVisualClientRpc(orbId);
            return true;
        }

        // 클라이언트 전용: PlayerPickupController가 거리 체크 시 사용
        public List<ulong> GetNearbyOrbIds(Vector3 position, float radius)
        {
            var result = new List<ulong>();
            float sqrRadius = radius * radius;
            foreach (var (id, go) in orbVisuals)
            {
                if (go == null) continue;
                if (Vector3.SqrMagnitude(go.transform.position - position) <= sqrRadius)
                    result.Add(id);
            }
            return result;
        }

        [ClientRpc]
        private void SpawnOrbVisualClientRpc(ulong id, Vector3 position)
        {
            if (orbVisualPrefab == null) return;
            var go = Instantiate(orbVisualPrefab, position, Quaternion.identity);
            var proxy = go.AddComponent<XPOrbVisualProxy>();
            proxy.Initialize(id);
            orbVisuals[id] = go;
        }

        [ClientRpc]
        private void DestroyOrbVisualClientRpc(ulong id)
        {
            if (!orbVisuals.TryGetValue(id, out var go)) return;
            orbVisuals.Remove(id);
            if (go != null) Destroy(go);
        }

        private readonly struct XPOrbEntry
        {
            public ulong Id  { get; }
            public Vector3 Pos { get; }
            public int Xp  { get; }

            public XPOrbEntry(ulong id, Vector3 pos, int xp)
            {
                Id  = id;
                Pos = pos;
                Xp  = xp;
            }
        }
    }
}
