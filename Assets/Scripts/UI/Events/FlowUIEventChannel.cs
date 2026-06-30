using System;

namespace Vamsurlike.UI.Events
{
    public sealed class FlowUIEventChannel
    {
        public event Action<GameFlowPayload>    GameFlowChanged;
        public event Action<MatchResultPayload> MatchResultReceived;
        public event Action                     StageResultRequested;

        public void PublishGameFlowChanged(GameFlowPayload p)    => GameFlowChanged?.Invoke(p);
        public void PublishMatchResult(MatchResultPayload p)      => MatchResultReceived?.Invoke(p);
        public void PublishStageResultRequested()                 => StageResultRequested?.Invoke();
    }
}
