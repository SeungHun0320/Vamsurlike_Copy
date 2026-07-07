using System;
using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Network
{
    [RequireComponent(typeof(NetworkManager))]
    public class PoolManager : MonoBehaviour
    {
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

        [SerializeField] private GOPoolConfig[] goConfigs;
        [SerializeField] private NetworkPoolConfig[] networkConfigs;
        [SerializeField] private NetworkPoolConfig[] deferredNetworkConfigs;

        private NetworkManager networkManager;
        private IGameObjectPool gameObjectPool;
        private INetworkObjectPool networkObjectPool;

        public static PoolManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }

            Instance = this;
            networkManager = GetComponent<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError($"[{nameof(PoolManager)}] NetworkManager가 없습니다.", this);
                enabled = false;
                return;
            }

            gameObjectPool = new GameObjectPool();
            networkObjectPool = new NetworkObjectPool(networkManager, transform);
        }

        private void OnEnable()
        {
            if (networkManager == null) return;

            networkManager.OnServerStarted += HandleNetworkStarted;
            networkManager.OnClientStarted += HandleNetworkStarted;
        }

        private void Start()
        {
            WarmupGameObjectPools();
        }

        private void OnDisable()
        {
            if (networkManager == null) return;

            networkManager.OnServerStarted -= HandleNetworkStarted;
            networkManager.OnClientStarted -= HandleNetworkStarted;
        }

        private void OnDestroy()
        {
            networkObjectPool?.Dispose();
            if (Instance == this) Instance = null;
        }

        public void WarmupDeferredPools()
        {
            if (deferredNetworkConfigs == null || networkManager == null || !networkManager.IsServer)
                return;

            foreach (NetworkPoolConfig config in deferredNetworkConfigs)
            {
                if (!IsValid(config)) continue;
                networkObjectPool?.Warmup(config.prefab, config.warmupCount);
            }
        }

        public GameObject GetGO(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return gameObjectPool?.Get(prefab, position, rotation);
        }

        public void ReturnGO(GameObject prefab, GameObject instance)
        {
            gameObjectPool?.Return(prefab, instance);
        }

        public void RegisterNetworkPrefab(GameObject prefab, int warmupCount = 0)
        {
            networkObjectPool?.RegisterPrefab(prefab, warmupCount);
        }

        public NetworkObject GetNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return networkObjectPool?.Get(prefab, position, rotation);
        }

        public static NetworkObject GetOrInstantiateNetworkObject(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            string context)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[{context}] prefab is null.");
                return null;
            }

            if (Instance != null && Instance.networkObjectPool != null)
                return Instance.GetNetworkObject(prefab, position, rotation);

            Debug.LogWarning(
                $"[{context}] PoolManager.Instance가 준비되지 않아 풀 없이 직접 Instantiate합니다. prefab={prefab.name}");

            GameObject instance = Instantiate(prefab, position, rotation);
            if (instance.TryGetComponent(out NetworkObject networkObject))
                return networkObject;

            Debug.LogWarning($"[{context}] prefab has no NetworkObject. prefab={prefab.name}", instance);
            Destroy(instance);
            return null;
        }

        public void ReturnNetworkObject(GameObject prefab, NetworkObject instance)
        {
            networkObjectPool?.Return(prefab, instance);
        }

        // GameObject(비네트워크) 풀 버전 — PoolManager.Instance가 아직 없을 때(초기화 순서 이슈)만
        // 경고 로그와 함께 직접 Instantiate/Destroy로 폴백한다. 정상 상황에서는 항상 풀을 거친다.
        public static GameObject GetOrInstantiateGO(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            string context)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[{context}] prefab is null.");
                return null;
            }

            if (Instance != null)
                return Instance.GetGO(prefab, position, rotation);

            Debug.LogWarning(
                $"[{context}] PoolManager.Instance가 준비되지 않아 풀 없이 직접 Instantiate합니다. prefab={prefab.name}");
            return Instantiate(prefab, position, rotation);
        }

        public static void ReturnOrDestroyGO(GameObject prefab, GameObject instance, string context)
        {
            if (instance == null) return;

            if (prefab != null && Instance != null)
            {
                Instance.ReturnGO(prefab, instance);
                return;
            }

            if (prefab == null)
                Debug.LogWarning($"[{context}] prefab이 없어 풀에 반환하지 못하고 직접 Destroy합니다.");
            else
                Debug.LogWarning(
                    $"[{context}] PoolManager.Instance가 없어 풀에 반환하지 못하고 직접 Destroy합니다. prefab={prefab.name}");
            Destroy(instance);
        }

        private void HandleNetworkStarted()
        {
            RegisterConfiguredPools(networkConfigs, true);
            RegisterConfiguredPools(deferredNetworkConfigs, false);
        }

        private void WarmupGameObjectPools()
        {
            if (goConfigs == null) return;

            foreach (GOPoolConfig config in goConfigs)
            {
                if (!IsValid(config)) continue;
                gameObjectPool?.Warmup(config.prefab, config.warmupCount);
            }
        }

        private void RegisterConfiguredPools(NetworkPoolConfig[] configs, bool warmupImmediately)
        {
            if (configs == null) return;

            foreach (NetworkPoolConfig config in configs)
            {
                if (!IsValid(config)) continue;
                int warmupCount = warmupImmediately ? config.warmupCount : 0;
                networkObjectPool?.RegisterPrefab(config.prefab, warmupCount);
            }
        }

        public bool Validate()
        {
            bool valid = true;
            valid &= ValidateConfigs(goConfigs,              nameof(goConfigs));
            valid &= ValidateConfigs(networkConfigs,         nameof(networkConfigs));
            valid &= ValidateConfigs(deferredNetworkConfigs, nameof(deferredNetworkConfigs));
            return valid;
        }

        private bool ValidateConfigs(GOPoolConfig[] configs, string fieldName)
        {
            if (configs == null) return true;
            bool valid = true;
            for (int i = 0; i < configs.Length; i++)
            {
                if (configs[i] == null || configs[i].prefab == null)
                {
                    Debug.LogError($"[{nameof(PoolManager)}] {fieldName}[{i}].prefab이 null입니다.", this);
                    valid = false;
                }
            }
            return valid;
        }

        private bool ValidateConfigs(NetworkPoolConfig[] configs, string fieldName)
        {
            if (configs == null) return true;
            bool valid = true;
            for (int i = 0; i < configs.Length; i++)
            {
                if (configs[i] == null || configs[i].prefab == null)
                {
                    Debug.LogError($"[{nameof(PoolManager)}] {fieldName}[{i}].prefab이 null입니다.", this);
                    valid = false;
                }
            }
            return valid;
        }

        private static bool IsValid(GOPoolConfig config)
        {
            return config != null && config.prefab != null && config.warmupCount >= 0;
        }

        private static bool IsValid(NetworkPoolConfig config)
        {
            return config != null && config.prefab != null && config.warmupCount >= 0;
        }
    }
}
