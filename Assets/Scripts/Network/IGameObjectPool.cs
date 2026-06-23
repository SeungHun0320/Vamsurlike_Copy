using UnityEngine;

namespace Vamsurlike.Network
{
    internal interface IGameObjectPool
    {
        void Warmup(GameObject prefab, int count);
        GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation);
        void Return(GameObject prefab, GameObject instance);
    }
}
