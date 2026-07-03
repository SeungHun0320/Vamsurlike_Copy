using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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

            // NetworkObject는 스폰되기 전엔 Transform.SetParent가 금지되어 있어(NGO 제약),
            // poolRoot 밑에 부모로 붙이지 않고 대기 중엔 poolRoot와 같은 씬(DontDestroyOnLoad)으로만
            // 옮겨 둔다 — 부모-자식 관계가 아니라 씬 소속만으로 대기/재사용 상태를 구분한다.
            Scene ddolScene = poolRoot.gameObject.scene;

            int warmedCount = 0;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    GameObject instance = UnityEngine.Object.Instantiate(prefab);
                    SceneManager.MoveGameObjectToScene(instance, ddolScene);
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

                // 대기 중엔 DontDestroyOnLoad 씬에 있던 오브젝트라, 체크아웃할 때 실제
                // 플레이 중인 씬으로 옮겨야 스테이지 재시작(Single 모드 씬 재로드) 시 정상적으로
                // 함께 파괴된다 — 그렇지 않으면 DDOL에 남아 다음 판까지 잔존한다.
                // SetParent가 아니라 씬 이동만 하는 이유: NetworkObject는 스폰 전 재부모 설정이
                // 금지되어 있다("NetworkObject can only be re-parented after being spawned").
                SceneManager.MoveGameObjectToScene(instance.gameObject, SceneManager.GetActiveScene());

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
            // poolRoot가 속한 DontDestroyOnLoad 씬으로 되돌려 둬야 다음 스테이지에서도
            // 재사용 가능한 상태로 안전하게 보관된다 (부모 설정이 아니라 씬 이동만 한다).
            SceneManager.MoveGameObjectToScene(instance.gameObject, poolRoot.gameObject.scene);
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
