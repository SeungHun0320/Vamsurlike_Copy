using System.Collections.Generic;
using UnityEngine;

namespace Vamsurlike.Network
{
    internal sealed class GameObjectPool : IGameObjectPool
    {
        private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();

        public void Warmup(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            Queue<GameObject> pool = GetOrCreatePool(prefab);
            for (int i = 0; i < count; i++)
            {
                GameObject instance = Object.Instantiate(prefab);
                instance.SetActive(false);
                pool.Enqueue(instance);
            }
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[{nameof(GameObjectPool)}] prefab이 없어 오브젝트를 가져올 수 없습니다.");
                return null;
            }

            Queue<GameObject> pool = GetOrCreatePool(prefab);
            while (pool.Count > 0)
            {
                GameObject instance = pool.Dequeue();
                if (instance == null) continue;

                instance.transform.SetPositionAndRotation(position, rotation);
                instance.SetActive(true);
                return instance;
            }

            return Object.Instantiate(prefab, position, rotation);
        }

        public void Return(GameObject prefab, GameObject instance)
        {
            if (prefab == null || instance == null) return;

            instance.SetActive(false);
            GetOrCreatePool(prefab).Enqueue(instance);
        }

        private Queue<GameObject> GetOrCreatePool(GameObject prefab)
        {
            if (!pools.TryGetValue(prefab, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                pools[prefab] = pool;
            }

            return pool;
        }
    }
}
