using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Stage;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.Adapters
{
    // SharedLevelSystem NetworkVariable 변화를 UIEventHub.Stage.SharedLevelChanged 이벤트로 변환하는 Adapter.
    // Stage 씬의 Canvas 또는 Manager 오브젝트에 배치.
    // SharedLevelSystem(NetworkObject)이 스폰 완료된 뒤 구독하므로 Update()에서 초기화를 시도한다.
    public class SharedLevelAdapter : MonoBehaviour
    {
        private bool subscribed;

        private void Update()
        {
            if (subscribed) return;
            if (SharedLevelSystem.Instance == null) return;

            SharedLevelSystem.Instance.SharedXP.OnValueChanged    += OnXPChanged;
            SharedLevelSystem.Instance.SharedLevel.OnValueChanged += OnLevelChanged;
            subscribed = true;
            Publish();
        }

        private void OnDisable()
        {
            if (!subscribed) return;
            if (SharedLevelSystem.Instance != null)
            {
                SharedLevelSystem.Instance.SharedXP.OnValueChanged    -= OnXPChanged;
                SharedLevelSystem.Instance.SharedLevel.OnValueChanged -= OnLevelChanged;
            }
            subscribed = false;
        }

        private void OnXPChanged(float _, float __)  => Publish();
        private void OnLevelChanged(int  _, int  __) => Publish();

        private void Publish()
        {
            if (SharedLevelSystem.Instance == null || UIEventHub.Instance == null) return;

            int   level      = SharedLevelSystem.Instance.SharedLevel.Value;
            float xp         = SharedLevelSystem.Instance.SharedXP.Value;
            float xpRequired = SharedLevelSystem.XPRequired(level);

            UIEventHub.Instance.Stage.PublishSharedLevel(
                new SharedLevelPayload(level, xp, xpRequired));
        }
    }
}
