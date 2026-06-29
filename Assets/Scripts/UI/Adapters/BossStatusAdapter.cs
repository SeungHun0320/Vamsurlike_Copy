using Unity.Netcode;
using Vamsurlike.Enemy;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI.Adapters
{
    // 보스 프리팹에 배치.
    // 자신의 EnemyNetworkBase.HP를 구독해 UIEventHub.Stage.BossStatusChanged 발행.
    public sealed class BossStatusAdapter : NetworkBehaviour
    {
        private EnemyNetworkBase enemy;

        private void Awake()
        {
            enemy = GetComponent<EnemyNetworkBase>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsClient) return;
            if (enemy == null) return;

            enemy.HP.OnValueChanged += OnHpChanged;
            enemy.MaxHPValue.OnValueChanged += OnMaxHpChanged;
            Publish();
        }

        public override void OnNetworkDespawn()
        {
            if (IsClient && enemy != null)
            {
                enemy.HP.OnValueChanged -= OnHpChanged;
                enemy.MaxHPValue.OnValueChanged -= OnMaxHpChanged;
                PublishHidden();
            }

            base.OnNetworkDespawn();
        }

        private void OnHpChanged(float _, float current)
        {
            if (current <= 0f) PublishHidden();
            else               Publish();
        }

        private void OnMaxHpChanged(float _, float __)
        {
            if (enemy != null && enemy.HP.Value > 0f)
                Publish();
        }

        private void Publish()
        {
            if (UIEventHub.Instance == null || enemy == null) return;
            float maxHp = enemy.MaxHP > 0f ? enemy.MaxHP : 1f;
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
