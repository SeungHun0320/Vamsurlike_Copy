using System;

namespace Vamsurlike.UI.Events
{
    public sealed class StageUIEventChannel
    {
        public event Action<StageTimerPayload>     StageTimerChanged;
        public event Action<BossStatusPayload>     BossStatusChanged;
        public event Action<SharedLevelPayload>    SharedLevelChanged;
        public event Action<AcquisitionLogPayload> AcquisitionLogRequested;

        public void PublishStageTimer(StageTimerPayload p)         => StageTimerChanged?.Invoke(p);
        public void PublishBossStatus(BossStatusPayload p)         => BossStatusChanged?.Invoke(p);
        public void PublishSharedLevel(SharedLevelPayload p)       => SharedLevelChanged?.Invoke(p);
        public void PublishAcquisitionLog(AcquisitionLogPayload p) => AcquisitionLogRequested?.Invoke(p);
    }
}
