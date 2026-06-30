using System;

namespace Vamsurlike.Network
{
    internal interface INetworkSessionService : IDisposable
    {
        string CurrentIp { get; }
        ushort CurrentPort { get; }

        void ConfigureConnectionApproval();
        bool StartClient(string ip, ushort port, string nickname = "");
        bool StartRelayClient();
        bool StartServer(string ip, ushort port);
        void Disconnect();
    }
}
