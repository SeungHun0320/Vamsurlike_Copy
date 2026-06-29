using System;
using Unity.Netcode;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.ViewModels
{
    // 로컬 플레이어의 PlayerStatus + 공유 레벨 상태를 HUD View에 전달.
    public sealed class HUDViewModel : ViewModelBase
    {
        public event Action<PlayerStatusPayload> OnLocalPlayerStatus;
        public event Action<SharedLevelPayload>  OnSharedLevel;

        // View 활성 시 최근 상태로 즉시 렌더링할 수 있도록 캐시
        public PlayerStatusPayload LastPlayerStatus { get; private set; }
        public SharedLevelPayload  LastSharedLevel  { get; private set; }

        protected override void Subscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Player.PlayerStatusChanged += HandlePlayerStatus;
            UIEventHub.Instance.Stage.SharedLevelChanged   += HandleSharedLevel;
        }

        protected override void Unsubscribe()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Player.PlayerStatusChanged -= HandlePlayerStatus;
            UIEventHub.Instance.Stage.SharedLevelChanged   -= HandleSharedLevel;
        }

        private void HandlePlayerStatus(PlayerStatusPayload payload)
        {
            // 네트워크 세션 중일 때만 ClientId로 필터링. 미연결 시 전체 허용.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (payload.ClientId != NetworkManager.Singleton.LocalClientId) return;
            }

            LastPlayerStatus = payload;
            OnLocalPlayerStatus?.Invoke(payload);
        }

        private void HandleSharedLevel(SharedLevelPayload payload)
        {
            LastSharedLevel = payload;
            OnSharedLevel?.Invoke(payload);
        }
    }
}
