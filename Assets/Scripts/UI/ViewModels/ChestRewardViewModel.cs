using System;
using Unity.Netcode;
using Vamsurlike.Items;
using Vamsurlike.Player;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.ViewModels
{
    public class ChestRewardViewModel : ViewModelBase
    {
        public event Action<ChestOptionsPayload> OnShow;
        public event Action                      OnHide;

        protected override void Subscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Reward.ChestOptionsReceived += HandleShow;
            UIEventHub.Instance.Reward.ChestRewardCompleted += HandleHide;
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Reward.ChestOptionsReceived -= HandleShow;
            UIEventHub.Instance.Reward.ChestRewardCompleted -= HandleHide;
        }

        private void HandleShow(ChestOptionsPayload payload)
        {
            if (!CanLocalPlayerChoose()) { OnHide?.Invoke(); return; }
            OnShow?.Invoke(payload);
        }

        private void HandleHide() => OnHide?.Invoke();

        // View → 카드 선택 시 호출하는 커맨드
        public void SubmitChoice(int cardIndex)
        {
            if (!CanLocalPlayerChoose()) return;
            ChestRewardManager.Instance?.SubmitChoiceServerRpc(cardIndex);
            OnHide?.Invoke(); // 서버 응답 대기 없이 즉시 닫기 (낙관적 업데이트)
        }

        private static bool CanLocalPlayerChoose()
        {
            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var stats = localPlayer != null ? localPlayer.GetComponent<PlayerNetworkStats>() : null;
            return stats != null && stats.CanAct;
        }
    }
}
