using UnityEngine;
using Vamsurlike.Enemy;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.Adapters
{
    // 보스 프리팹에 배치.
    // 자신의 EnemyNetworkBase.HP를 구독해 UIEventHub.Stage.BossStatusChanged 발행.
    public sealed class BossStatusAdapter : MonoBehaviour
    {
        private EnemyNetworkBase enemy;

        private void Start()
        {
            enemy = GetComponent<EnemyNetworkBase>();
            if (enemy == null) return;
            enemy.HP.OnValueChanged += OnHpChanged;
            Publish();
        }

        private void OnDestroy()
        {
            if (enemy != null)
                enemy.HP.OnValueChanged -= OnHpChanged;
            PublishHidden();
        }

        private void OnHpChanged(float _, float current)
        {
            if (current <= 0f) PublishHidden();
            else               Publish();
        }

        private void Publish()
        {
            if (UIEventHub.Instance == null || enemy == null) return;
            float maxHp = enemy.Data != null ? enemy.Data.hp : 1f;
            UIEventHub.Instance.Stage.PublishBossStatus(
                new BossStatusPayload(true, enemy.HP.Value, maxHp));
        }

        private void PublishHidden()
        {
            if (UIEventHub.Instance == null) return;
            UIEventHub.Instance.Stage.PublishBossStatus(new BossStatusPayload(false, 0f, 1f));
        }
    }
}
