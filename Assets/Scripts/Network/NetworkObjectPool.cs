using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Network
{
    internal sealed class NetworkObjectPool : INetworkObjectPool
    {
        private readonly NetworkManager networkManager;
        private readonly Transform poolRoot;
        private readonly Dictionary<GameObject, Stack<NetworkObject>> pools = new();
        private readonly HashSet<GameObject> registeredPrefabs = new();

        public NetworkObjectPool(NetworkManager networkManager, Transform poolRoot)
        {
            this.networkManager = networkManager;
            this.poolRoot = poolRoot;
        }

        public void RegisterPrefab(GameObject prefab, int warmupCount = 0)
        {
            if (prefab == null || networkManager == null) return;

            GetOrCreatePool(prefab);
            if (!registeredPrefabs.Add(prefab)) return;

            try
            {
                networkManager.PrefabHandler.AddHandler(
                    prefab,
                    new NetworkPrefabInstanceHandler(prefab, this));
            }
            catch (Exception exception)
            {
                registeredPrefabs.Remove(prefab);
                Debug.LogError(
                    $"[{nameof(NetworkObjectPool)}] {prefab.name} PrefabHandler 등록 실패: {exception.Message}");
                return;
            }

            if (warmupCount > 0 && networkManager.IsServer)
                Warmup(prefab, warmupCount);
        }

        public void Warmup(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            Stack<NetworkObject> pool = GetOrCreatePool(prefab);
            bool wasActive = prefab.activeSelf;
            if (wasActive) prefab.SetActive(false);

            int warmedCount = 0;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    GameObject instance = UnityEngine.Object.Instantiate(prefab, poolRoot);
                    if (instance.TryGetComponent(out NetworkObject networkObject))
                    {
                        pool.Push(networkObject);
                        warmedCount++;
                    }
                    else
                    {
                        Debug.LogError($"[{nameof(NetworkObjectPool)}] {prefab.name}에 NetworkObject가 없습니다.");
                        UnityEngine.Object.Destroy(instance);
                    }
                }
            }
            finally
            {
                if (wasActive) prefab.SetActive(true);
            }

        }

        public NetworkObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[{nameof(NetworkObjectPool)}] prefab이 없어 네트워크 오브젝트를 가져올 수 없습니다.");
                return null;
            }

            Stack<NetworkObject> pool = GetOrCreatePool(prefab);
            while (pool.Count > 0)
            {
                NetworkObject instance = pool.Pop();
                if (instance == null) continue;

                instance.transform.SetPositionAndRotation(position, rotation);
                instance.gameObject.SetActive(true);
                return instance;
            }

            return Create(prefab, position, rotation);
        }

        public void Return(GameObject prefab, NetworkObject instance)
        {
            if (prefab == null || instance == null) return;

            instance.gameObject.SetActive(false);
            GetOrCreatePool(prefab).Push(instance);
        }

        public void Dispose()
        {
            if (networkManager != null)
            {
                foreach (GameObject prefab in registeredPrefabs)
                {
                    if (prefab != null)
                        networkManager.PrefabHandler.RemoveHandler(prefab);
                }
            }

            registeredPrefabs.Clear();
        }

        private Stack<NetworkObject> GetOrCreatePool(GameObject prefab)
        {
            if (!pools.TryGetValue(prefab, out Stack<NetworkObject> pool))
            {
                pool = new Stack<NetworkObject>();
                pools[prefab] = pool;
            }

            return pool;
        }

        private static NetworkObject Create(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
            if (instance.TryGetComponent(out NetworkObject networkObject)) return networkObject;

            Debug.LogError($"[{nameof(NetworkObjectPool)}] {prefab.name}에 NetworkObject가 없습니다.");
            UnityEngine.Object.Destroy(instance);
            return null;
        }
    }
}
