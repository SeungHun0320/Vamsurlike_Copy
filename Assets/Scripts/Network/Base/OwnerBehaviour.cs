using Unity.Netcode;

namespace Vamsurlike.Network
{
    /// <summary>
    /// 로컬 오너(IsOwner)에서만 활성화되는 NetworkBehaviour 베이스.
    /// 다른 플레이어 인스턴스와 서버에서는 enabled = false.
    /// </summary>
    public abstract class OwnerBehaviour : NetworkBehaviour
    {
        public sealed override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            OnOwnerSpawned();
        }

        public sealed override void OnNetworkDespawn()
        {
            if (IsOwner) OnOwnerDespawned();
            base.OnNetworkDespawn();
        }

        protected virtual void OnOwnerSpawned() { }
        protected virtual void OnOwnerDespawned() { }
    }
}
