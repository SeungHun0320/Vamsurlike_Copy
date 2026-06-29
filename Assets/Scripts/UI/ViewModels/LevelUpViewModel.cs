using System;
using Unity.Netcode;
using Vamsurlike.Player;
using Vamsurlike.UI.Events;
using Vamsurlike.Upgrades;

namespace Vamsurlike.UI.ViewModels
{
    public class LevelUpViewModel : ViewModelBase
    {
        public event Action<LevelUpOptionsPayload> OnShow;
        public event Action                        OnHide;

        protected override void Subscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Reward.LevelUpOptionsReceived += HandleShow;
            UIEventHub.Instance.Reward.LevelUpCompleted       += HandleHide;
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Reward.LevelUpOptionsReceived -= HandleShow;
            UIEventHub.Instance.Reward.LevelUpCompleted       -= HandleHide;
        }

        private void HandleShow(LevelUpOptionsPayload payload)
        {
            if (!CanLocalPlayerChoose()) { OnHide?.Invoke(); return; }
            OnShow?.Invoke(payload);
        }

        private void HandleHide() => OnHide?.Invoke();

        // View → 카드 선택 시 호출하는 커맨드
        public void SubmitChoice(int cardIndex)
        {
            if (!CanLocalPlayerChoose()) return;
            LevelUpManager.Instance?.SubmitChoiceServerRpc(cardIndex);
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
