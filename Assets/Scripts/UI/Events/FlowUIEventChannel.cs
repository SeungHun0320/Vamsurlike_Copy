using System;

namespace Vamsurlike.UI.Events
{
    public sealed class FlowUIEventChannel
    {
        public event Action<GameFlowPayload> GameFlowChanged;

        public void PublishGameFlowChanged(GameFlowPayload p) => GameFlowChanged?.Invoke(p);
    }
}
