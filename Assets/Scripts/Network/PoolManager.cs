using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Network
{
    // Bootstrap NetworkManager 오브젝트에 배치.
    // - GOPool  : 일반 GameObject (VFX, 파티클, UI 등)
    // - NetPool : NGO NetworkObject (적, 투사체 등) — PrefabHandler 통합
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        [Serializable]
        public class GOPoolConfig
        {
            public GameObject prefab;
            [Min(0)] public int warmupCount = 10;
        }

        [Serializable]
        public class NetworkPoolConfig
        {
            public GameObject prefab;
            [Min(0)] public int warmupCount = 20;
        }

        [SerializeField] private GOPoolConfig[]      goConfigs;
        [SerializeField] private NetworkPoolConfig[] networkConfigs;

        private readonly Dictionary<GameObject, Queue<GameObject>>    goPools          = new();
        private readonly Dictionary<GameObject, Stack<NetworkObject>> netPools         = new();
        private readonly HashSet<GameObject>                          registeredPrefabs = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
        }

        private void Start()
        {
            foreach (var cfg in goConfigs)
                WarmupGO(cfg.prefab, cfg.warmupCount);

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted += OnNetworkStarted;
                NetworkManager.Singleton.OnClientStarted += OnNetworkStarted;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted -= OnNetworkStarted;
                NetworkManager.Singleton.OnClientStarted -= OnNetworkStarted;

                foreach (var prefab in registeredPrefabs)
                    if (prefab != null)
                        NetworkManager.Singleton.PrefabHandler.RemoveHandler(prefab);
            }
            registeredPrefabs.Clear();
        }

        // ─── 이벤트 ────────────────────────────────────────────────────────────

        private void OnNetworkStarted()
        {
            foreach (var cfg in networkConfigs)
                RegisterNetworkPrefab(cfg.prefab, cfg.warmupCount);
        }

        // ─── 일반 GO 풀 ────────────────────────────────────────────────────────

        public GameObject GetGO(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (goPools.TryGetValue(prefab, out var queue) && queue.Count > 0)
            {
                var go = queue.Dequeue();
                go.transform.SetPositionAndRotation(pos, rot);
                go.SetActive(true);
                return go;
            }
            return Instantiate(prefab, pos, rot);
        }

        public void ReturnGO(GameObject prefab, GameObject go)
        {
            go.SetActive(false);
            if (!goPools.TryGetValue(prefab, out var queue))
                goPools[prefab] = queue = new Queue<GameObject>();
            queue.Enqueue(go);
        }

        // ─── NetworkObject 풀 ──────────────────────────────────────────────────

        // 런타임 추가 등록 (스테이지 로드 시 호출 가능)
        // warmupCount: 현재 미사용 — Despawn(false) 재사용으로 풀이 런타임에 유기적으로 채워짐
        public void RegisterNetworkPrefab(GameObject prefab, int warmupCount = 0)
        {
            if (prefab == null || NetworkManager.Singleton == null) return;
            if (!netPools.ContainsKey(prefab))
                netPools[prefab] = new Stack<NetworkObject>();

            // Host 모드에서 OnServerStarted + OnClientStarted가 모두 발화해 이중 등록 방지
            if (!registeredPrefabs.Add(prefab)) return;

            NetworkManager.Singleton.PrefabHandler.AddHandler(
                prefab, new NetPrefabHandler(prefab, this));
        }

        public NetworkObject GetNetworkObject(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (netPools.TryGetValue(prefab, out var stack))
            {
                while (stack.Count > 0)
                {
                    var pooled = stack.Pop();
                    if (pooled == null) continue; // NGO가 파괴한 오브젝트 건너뜀
                    pooled.transform.SetPositionAndRotation(pos, rot);
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }
            return CreateNetworkObject(prefab, pos, rot);
        }

        public void ReturnNetworkObject(GameObject prefab, NetworkObject obj)
        {
            if (obj == null) return;
            obj.gameObject.SetActive(false);
            if (!netPools.TryGetValue(prefab, out var stack))
                netPools[prefab] = stack = new Stack<NetworkObject>();
            stack.Push(obj);
        }

        // ─── 내부 헬퍼 ────────────────────────────────────────────────────────

        private void WarmupGO(GameObject prefab, int count)
        {
            if (prefab == null) return;
            if (!goPools.ContainsKey(prefab))
                goPools[prefab] = new Queue<GameObject>();

            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(prefab);
                go.SetActive(false);
                goPools[prefab].Enqueue(go);
            }
        }

        private NetworkObject CreateNetworkObject(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            var go = Instantiate(prefab, pos, rot);
            if (go.TryGetComponent<NetworkObject>(out var obj)) return obj;
            Debug.LogError($"[PoolManager] {prefab.name} 에 NetworkObject 없음");
            Destroy(go);
            return null;
        }

        // ─── NGO PrefabHandler ─────────────────────────────────────────────────

        private sealed class NetPrefabHandler : INetworkPrefabInstanceHandler
        {
            private readonly GameObject  prefab;
            private readonly PoolManager pool;

            internal NetPrefabHandler(GameObject prefab, PoolManager pool)
            {
                this.prefab = prefab;
                this.pool   = pool;
            }

            public NetworkObject Instantiate(ulong ownerClientId, Vector3 pos, Quaternion rot)
                => pool.GetNetworkObject(prefab, pos, rot);

            public void Destroy(NetworkObject obj)
                => pool.ReturnNetworkObject(prefab, obj);
        }
    }
}
