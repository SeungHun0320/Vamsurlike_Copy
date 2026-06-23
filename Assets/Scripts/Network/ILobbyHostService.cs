using System;

namespace Vamsurlike.Network
{
    internal interface ILobbyHostService : IDisposable
    {
        event Action<ulong> LobbyHostChanged;

        ulong LobbyHostClientId { get; }
        bool IsLocalLobbyHost { get; }

        void RegisterMessageHandler();
        void UnregisterMessageHandler();
        void HandleClientConnected(ulong clientId);
        void HandleClientDisconnected(ulong clientId);
        void Clear();
    }
}
