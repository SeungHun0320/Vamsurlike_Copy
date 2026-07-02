using System;

namespace Vamsurlike.Network
{
    internal interface IGameStartService : IDisposable
    {
        void RegisterMessageHandler();
        void UnregisterMessageHandler();
        bool RequestStartGame();
        bool RequestReturnToLobby();
    }
}
