using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Network
{
    internal interface INetworkObjectPool : IDisposable
    {
        void RegisterPrefab(GameObject prefab, int warmupCount = 0);
        void Warmup(GameObject prefab, int count);
        IEnumerator WarmupOverFrames(GameObject prefab, int count, int perFrame);
        NetworkObject Get(GameObject prefab, Vector3 position, Quaternion rotation);
        void Return(GameObject prefab, NetworkObject instance);
    }
}
