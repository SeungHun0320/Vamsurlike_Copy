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
