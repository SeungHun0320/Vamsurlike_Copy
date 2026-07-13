using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Audio;
using Vamsurlike.Network;
using Vamsurlike.Player;
using Vamsurlike.Upgrades;
using Vamsurlike.VFX;

namespace Vamsurlike.Stage
{
    // Stage 씬의 NetworkObject로 배치. XPOrbManager와 동일 패턴(NetworkObject 아닌 서버 데이터
    // 목록 + 클라이언트 비주얼 프록시)이다.
    // 정정(완전 공유) — 골드는 XP처럼 누가 줍든 전원에게 동일한 기본량이 귀속된다. 플레이어 간
    // 유일한 차이는 각자의 PassiveStatHandler.GoldMultiplier(영구 업그레이드로 산 배율)뿐이며,
    // 이는 픽업 시점에 각자 자신의 값을 곱해서 개인별로 확정한다.
    public class GoldOrbManager : NetworkBehaviour
    {
        public static GoldOrbManager Instance { get; private set; }

        [SerializeField] private GameObject orbVisualPrefab;
        [SerializeField] private VFXSpawnEventSO vfxSpawnEvent;
        [SerializeField] private float pickupVFXDuration = 0.15f;
        [SerializeField] private SFXSpawnEventSO sfxSpawnEvent;

        [Header("Pickup Fly")]
        [SerializeField] private float flyDuration = 0.6f;
        [SerializeField] private AnimationCurve flyEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // 서버 전용
        private readonly Dictionary<ulong, GoldOrbEntry> activeOrbs = new();
        private ulong nextOrbId;

        // 클라이언트 전용: id → 비주얼 프록시 오브젝트
        private readonly Dictionary<ulong, GameObject> orbVisuals = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            foreach (var (_, go) in orbVisuals)
            {
                if (go == null) continue;
                if (go.TryGetComponent<GoldOrbVisualProxy>(out var proxy))
                    proxy.Clear();
                PoolManager.ReturnOrDestroyGO(orbVisualPrefab, go, nameof(GoldOrbManager));
            }
            orbVisuals.Clear();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        // 서버 전용: 적 사망 시 DropManager가 호출
        public void SpawnOrb(Vector3 position, int gold)
        {
            if (!IsServer || gold <= 0) return;
            ulong id = nextOrbId++;
            activeOrbs[id] = new GoldOrbEntry(id, position, gold);
            SpawnOrbVisualClientRpc(id, position);
        }

        // 서버 전용: PlayerPickupController.RequestPickupServerRpc에서 호출
        public bool TryPickup(ulong orbId, ulong clientId)
        {
            if (!IsServer) return false;
            if (!activeOrbs.TryGetValue(orbId, out var orb)) return false;
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return false;
            if (client.PlayerObject == null) return false;

            var stats = client.PlayerObject.GetComponent<PlayerNetworkStats>();

            // 사망/다운 상태 플레이어는 골드 픽업 불가 (XP 픽업과 동일 규칙)
            if (stats != null && !stats.CanAct) return false;

            // 서버 권한 거리 검증: 픽업 반경 기준 + 네트워크 지연 보상(×2)
            float pickupRadius = stats != null && stats.PickupRadius.Value > 0f
                ? stats.PickupRadius.Value : 2f;
            float maxSqrDist = (pickupRadius * 2f) * (pickupRadius * 2f);
            if (Vector3.SqrMagnitude(client.PlayerObject.transform.position - orb.Pos) > maxSqrDist)
                return false;

            activeOrbs.Remove(orbId);
            StartCoroutine(DelayedDistributeGold(orb.Gold, flyDuration));

            DestroyOrbVisualClientRpc(orbId, clientId, client.PlayerObject.transform.position);
            return true;
        }

        // 클라이언트에서 오브가 날아가는 연출(flyDuration)이 끝나는 시점에 맞춰 골드를 반영한다 —
        // 즉시 반영하면 오브가 아직 화면에서 날아가는 중인데 골드가 먼저 올라가는 어긋남이 생긴다.
        private IEnumerator DelayedDistributeGold(int baseGold, float delay)
        {
            yield return new WaitForSeconds(delay);
            DistributeGold(baseGold);
        }

        // 누가 주웠는지와 무관하게 접속 중인 모든 플레이어에게 동일한 기본량을 귀속시키고,
        // 각자 자신의 GoldMultiplier만 곱해서 개인별 최종량을 확정한다.
        private static void DistributeGold(int baseGold)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                if (!client.PlayerObject.TryGetComponent<PlayerMatchStats>(out var matchStats)) continue;

                float goldMultiplier = client.PlayerObject.TryGetComponent<PassiveStatHandler>(out var passive)
                    ? passive.GoldMultiplier.Value
                    : 1f;
                int finalGold = Mathf.Max(1, Mathf.RoundToInt(baseGold * goldMultiplier));

                matchStats.AddGold(finalGold);
            }
        }

        // 서버 전용: 자석 아이템 픽업 시 호출 — 거리 무관하게 씬에 남아있는 모든 골드 오브를 즉시 흡수한다.
        // 누가 주웠는지와 무관하게 전원에게 동일 기본량이 귀속되는 규칙(DistributeGold)은 그대로 유지되고,
        // collectorClientId는 화면상 "누구에게 날아가는지" 연출용으로만 쓰인다.
        public void CollectAll(ulong collectorClientId)
        {
            if (!IsServer || activeOrbs.Count == 0) return;

            bool hasCollector = NetworkManager.ConnectedClients.TryGetValue(collectorClientId, out var client)
                && client.PlayerObject != null;
            Vector3 collectorPosition = hasCollector ? client.PlayerObject.transform.position : Vector3.zero;

            // 순회 중 activeOrbs를 직접 지우면 안 되므로 키 목록을 먼저 복사한다.
            var ids = new List<ulong>(activeOrbs.Keys);
            foreach (ulong id in ids)
            {
                if (!activeOrbs.Remove(id, out GoldOrbEntry orb)) continue;
                StartCoroutine(DelayedDistributeGold(orb.Gold, flyDuration));
                DestroyOrbVisualClientRpc(id, collectorClientId, collectorPosition);
            }
        }

        // 클라이언트 전용: PlayerPickupController가 거리 체크 시 사용
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
            var go = PoolManager.GetOrInstantiateGO(
                orbVisualPrefab, position, Quaternion.identity, nameof(GoldOrbManager));
            if (go == null) return;

            if (!go.TryGetComponent<GoldOrbVisualProxy>(out var proxy))
                proxy = go.AddComponent<GoldOrbVisualProxy>();

            proxy.Initialize(id);
            proxy.ConfigureFly(flyDuration, flyEase);
            orbVisuals[id] = go;
        }

        [ClientRpc]
        private void DestroyOrbVisualClientRpc(ulong id, ulong collectorClientId, Vector3 collectorPosition)
        {
            if (!orbVisuals.TryGetValue(id, out var go)) return;
            orbVisuals.Remove(id);
            if (go == null) return;

            Transform collectorTransform = NetworkManager.Singleton != null
                && NetworkManager.Singleton.ConnectedClients.TryGetValue(collectorClientId, out var client)
                && client.PlayerObject != null
                    ? client.PlayerObject.transform
                    : null;

            if (go.TryGetComponent<GoldOrbVisualProxy>(out var proxy))
            {
                proxy.FlyToAndComplete(collectorTransform, collectorPosition, () => FinishOrbPickup(go, proxy));
                return;
            }

            FinishOrbPickup(go, null);
        }

        private void FinishOrbPickup(GameObject go, GoldOrbVisualProxy proxy)
        {
            if (go == null) return;

            vfxSpawnEvent?.Raise(new VFXCue(
                VFXCueIds.PickupAbsorb, go.transform.position, Vector3.up, 1f, pickupVFXDuration, Color.white));
            sfxSpawnEvent?.Raise(new SFXCue(SFXCueIds.GoldPickup, go.transform.position));

            proxy?.Clear();
            PoolManager.ReturnOrDestroyGO(orbVisualPrefab, go, nameof(GoldOrbManager));
        }

        private readonly struct GoldOrbEntry
        {
            public ulong   Id   { get; }
            public Vector3 Pos  { get; }
            public int     Gold { get; }

            public GoldOrbEntry(ulong id, Vector3 pos, int gold)
            {
                Id   = id;
                Pos  = pos;
                Gold = gold;
            }
        }
    }
}
