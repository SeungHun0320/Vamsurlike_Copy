using Unity.Netcode;
using UnityEngine;

namespace Vamsurlike.Network
{
    internal sealed class NetworkPrefabInstanceHandler : INetworkPrefabInstanceHandler
    {
        private readonly GameObject prefab;
        private readonly INetworkObjectPool pool;

        public NetworkPrefabInstanceHandler(GameObject prefab, INetworkObjectPool pool)
        {
            this.prefab = prefab;
            this.pool = pool;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            return pool.Get(prefab, position, rotation);
        }

        public void Destroy(NetworkObject networkObject)
        {
            pool.Return(prefab, networkObject);
        }
    }
}
