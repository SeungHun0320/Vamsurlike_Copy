using System;
using System.Collections.Generic;

namespace Vamsurlike.UI.Events
{
    public sealed class PlayerUIEventChannel
    {
        private readonly Dictionary<ulong, PlayerStatusPayload> latestPlayerStatuses = new();

        public event Action<PlayerStatusPayload>   PlayerStatusChanged;
        public event Action<ReviveProgressPayload> ReviveProgressChanged;
        public event Action                        PlayerRevived;

        public bool TryGetLatestPlayerStatus(ulong clientId, out PlayerStatusPayload payload) =>
            latestPlayerStatuses.TryGetValue(clientId, out payload);

        public bool TryGetAnyLatestPlayerStatus(out PlayerStatusPayload payload)
        {
            foreach (PlayerStatusPayload status in latestPlayerStatuses.Values)
            {
                payload = status;
                return true;
            }

            payload = default;
            return false;
        }

        public void PublishPlayerStatus(PlayerStatusPayload p)
        {
            latestPlayerStatuses[p.ClientId] = p;
            PlayerStatusChanged?.Invoke(p);
        }

        public void PublishReviveProgress(ReviveProgressPayload p) => ReviveProgressChanged?.Invoke(p);
        public void PublishPlayerRevived()                         => PlayerRevived?.Invoke();
    }
}
