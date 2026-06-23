using Unity.Netcode;

namespace Vamsurlike.Network
{
    /// <summary>
    /// 클라이언트(IsClient)에서만 활성화되는 NetworkBehaviour 베이스.
    /// 서버(데디케이티드 전용, 호스트 모드 미사용)에서는 enabled = false.
    /// </summary>
    public abstract class ClientBehaviour : NetworkBehaviour
    {
        public sealed override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            // 서버(데디케이티드 전용 — 호스트 모드 미사용)에서는 비활성화
            if (IsServer)
            {
                enabled = false;
                return;
            }
            OnClientSpawned();
        }

        public sealed override void OnNetworkDespawn()
        {
            if (!IsServer) OnClientDespawned();
            base.OnNetworkDespawn();
        }

        protected virtual void OnClientSpawned() { }
        protected virtual void OnClientDespawned() { }
    }
}
