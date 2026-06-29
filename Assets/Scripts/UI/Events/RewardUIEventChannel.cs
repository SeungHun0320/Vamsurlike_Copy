using System;

namespace Vamsurlike.UI.Events
{
    public sealed class RewardUIEventChannel
    {
        public event Action<LevelUpOptionsPayload> LevelUpOptionsReceived;
        public event Action                        LevelUpCompleted;
        public event Action<ChestOptionsPayload>   ChestOptionsReceived;
        public event Action                        ChestRewardCompleted;

        public void PublishLevelUpOptions(LevelUpOptionsPayload p) => LevelUpOptionsReceived?.Invoke(p);
        public void PublishLevelUpCompleted()                      => LevelUpCompleted?.Invoke();
        public void PublishChestOptions(ChestOptionsPayload p)     => ChestOptionsReceived?.Invoke(p);
        public void PublishChestRewardCompleted()                  => ChestRewardCompleted?.Invoke();
    }
}
