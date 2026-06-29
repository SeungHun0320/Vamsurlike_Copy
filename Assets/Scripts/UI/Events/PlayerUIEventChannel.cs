using System;

namespace Vamsurlike.UI.Events
{
    public sealed class PlayerUIEventChannel
    {
        public event Action<PlayerStatusPayload>   PlayerStatusChanged;
        public event Action<ReviveProgressPayload> ReviveProgressChanged;
        public event Action                        PlayerRevived;

        public void PublishPlayerStatus(PlayerStatusPayload p)     => PlayerStatusChanged?.Invoke(p);
        public void PublishReviveProgress(ReviveProgressPayload p) => ReviveProgressChanged?.Invoke(p);
        public void PublishPlayerRevived()                         => PlayerRevived?.Invoke();
    }
}
