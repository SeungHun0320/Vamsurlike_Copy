using System;

namespace Vamsurlike.UI.Events
{
    public sealed class StageUIEventChannel
    {
        private StageTimerPayload latestStageTimer;
        private BossStatusPayload latestBossStatus;
        private SharedLevelPayload latestSharedLevel;
        private bool hasStageTimer;
        private bool hasBossStatus;
        private bool hasSharedLevel;

        public event Action<StageTimerPayload>     StageTimerChanged;
        public event Action<BossStatusPayload>     BossStatusChanged;
        public event Action<SharedLevelPayload>    SharedLevelChanged;
        public event Action<AcquisitionLogPayload> AcquisitionLogRequested;

        public bool TryGetLatestStageTimer(out StageTimerPayload payload)
        {
            payload = latestStageTimer;
            return hasStageTimer;
        }

        public bool TryGetLatestBossStatus(out BossStatusPayload payload)
        {
            payload = latestBossStatus;
            return hasBossStatus;
        }

        public bool TryGetLatestSharedLevel(out SharedLevelPayload payload)
        {
            payload = latestSharedLevel;
            return hasSharedLevel;
        }

        public void PublishStageTimer(StageTimerPayload p)
        {
            latestStageTimer = p;
            hasStageTimer = true;
            StageTimerChanged?.Invoke(p);
        }

        public void PublishBossStatus(BossStatusPayload p)
        {
            latestBossStatus = p;
            hasBossStatus = true;
            BossStatusChanged?.Invoke(p);
        }

        public void PublishSharedLevel(SharedLevelPayload p)
        {
            latestSharedLevel = p;
            hasSharedLevel = true;
            SharedLevelChanged?.Invoke(p);
        }

        public void PublishAcquisitionLog(AcquisitionLogPayload p) => AcquisitionLogRequested?.Invoke(p);
    }
}
