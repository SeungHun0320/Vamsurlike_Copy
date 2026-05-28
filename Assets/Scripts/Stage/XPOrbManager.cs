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

        public override void OnDestroy()
        {
            base.OnDestroy();
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
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return false;

            // 서버 권한 거리 검증: 플레이어 스탯의 픽업 반경 기준 + 네트워크 지연 보상(×2)
            if (client.PlayerObject != null)
            {
                var stats = client.PlayerObject.GetComponent<PlayerNetworkStats>();
                float pickupRadius = stats != null && stats.PickupRadius.Value > 0f
                    ? stats.PickupRadius.Value : 2f;
                float maxSqrDist = (pickupRadius * 2f) * (pickupRadius * 2f);
                if (Vector3.SqrMagnitude(client.PlayerObject.transform.position - orb.Pos) > maxSqrDist)
                    return false;
            }

            activeOrbs.Remove(orbId);
            client.PlayerObject?.GetComponent<PlayerNetworkStats>()?.AddXP(orb.Xp);
            DestroyOrbVisualClientRpc(orbId);
            return true;
        }

        // 클라이언트 전용: PlayerPickupController가 거리 체크 시 사용
        // 호출자가 results를 소유 — 캐시 노출/중첩 호출 안전
        public void FillNearbyOrbIds(Vector3 position, float radius, List<ulong> results)
        {
            results.Clear();
            float sqrRadius = radius * radius;
            foreach (var (id, go) in orbVisuals)
            {
                if (go == null) continue;
                if (Vector3.SqrMagnitude(go.transform.position - position) <= sqrRadius)
                    results.Add(id);
            }
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
