using System;
using System.Collections;
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

        // 재시작 직후 한 프레임에 수십 개를 한꺼번에 Instantiate하면 그 프레임에 눈에 띄는 히치가
        // 생긴다 — perFrame개씩 나눠서 여러 프레임에 걸쳐 워밍업한다. (검증 전이라 아직 호출부는
        // 기존 동기 Warmup()을 그대로 씀 — 원인 확인 후 PoolManager 쪽에서 이걸로 교체 예정)
        public IEnumerator WarmupOverFrames(GameObject prefab, int count, int perFrame)
        {
            if (prefab == null || count <= 0) yield break;

            Stack<NetworkObject> pool = GetOrCreatePool(prefab);
            bool wasActive = prefab.activeSelf;
            if (wasActive) prefab.SetActive(false);

            Scene ddolScene = poolRoot.gameObject.scene;
            int spawnedThisBatch = 0;

            for (int i = 0; i < count; i++)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(instance, ddolScene);
                if (instance.TryGetComponent(out NetworkObject networkObject))
                {
                    pool.Push(networkObject);
                }
                else
                {
                    Debug.LogError($"[{nameof(NetworkObjectPool)}] {prefab.name}에 NetworkObject가 없습니다.");
                    UnityEngine.Object.Destroy(instance);
                }

                spawnedThisBatch++;
                if (spawnedThisBatch >= Mathf.Max(1, perFrame))
                {
                    spawnedThisBatch = 0;
                    yield return null;
                }
            }

            if (wasActive) prefab.SetActive(true);
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

                // 씬 언로드(재시작) 도중 아직 Despawn()을 못 받은 채로 파괴되는 오브젝트도 NGO가 자동으로
                // OnNetworkDespawn을 호출해 Return()이 실행될 수 있다 — 이때 들어온 인스턴스는 아직
                // IsSpawned=true(좀비 상태)라 그대로 재사용하면 NGO가 "이미 스폰됨"으로 거부하거나(적),
                // 위치만 옮겨진 채 이전 스폰 상태로 남아 클라이언트에서 순간이동처럼 보이는(총알) 문제로
                // 이어진다 — 폐기하고 다음 항목을 시도한다. (STAGE_RESTART_BUG_LOG.md 증상4, A/B 검증 완료)
                if (instance.IsSpawned)
                {
                    Debug.LogWarning(
                        $"[{nameof(NetworkObjectPool)}] {prefab.name} 풀에서 꺼낸 인스턴스가 이미 Spawn 상태 — 폐기하고 다음 시도.");
                    continue;
                }

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

            // 주의: 이 시점(OnNetworkDespawn 콜백 안에서 호출됨)에는 NGO가 아직 IsSpawned를 false로
            // 되돌리기 전이라 instance.IsSpawned==true인 게 정상 케이스다 — 여기서 IsSpawned를 걸러내면
            // 정상적인 반환까지 전부 막혀버린다. 상태 검증은 Get() 쪽에서만 한다.

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
