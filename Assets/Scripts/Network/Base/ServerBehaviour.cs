using Unity.Netcode;

namespace Vamsurlike.Network
{
    /// <summary>
    /// 서버(IsServer)에서만 활성화되는 NetworkBehaviour 베이스.
    /// 데디케이티드 서버와 호스트 모두에서 서버 로직을 실행한다.
    /// 클라이언트 인스턴스에서는 OnNetworkSpawn 시 enabled = false.
    /// </summary>
    public abstract class ServerBehaviour : NetworkBehaviour
    {
        public sealed override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer)
            {
                enabled = false;
                return;
            }
            OnServerSpawned();
        }

        public sealed override void OnNetworkDespawn()
        {
            if (IsServer) OnServerDespawned();
            base.OnNetworkDespawn();
        }

        protected virtual void OnServerSpawned() { }
        protected virtual void OnServerDespawned() { }
    }
}
