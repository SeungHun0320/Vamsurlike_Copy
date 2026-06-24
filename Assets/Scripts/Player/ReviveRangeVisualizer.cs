using UnityEngine;
using Vamsurlike.Network;
using Vamsurlike.Skills;

namespace Vamsurlike.Player
{
    // 클라이언트 전용: 다운된 플레이어의 부활 가능 범위를 표시한다.
    [RequireComponent(typeof(PlayerNetworkStats))]
    [RequireComponent(typeof(PlayerReviveHandler))]
    public sealed class ReviveRangeVisualizer : ClientBehaviour
    {
        [SerializeField] private Color rangeColor = new(0.15f, 0.85f, 1f, 0.9f);

        private PlayerNetworkStats stats;
        private PlayerReviveHandler reviveHandler;
        private GameObject rangeVisual;

        private void Awake()
        {
            stats = GetComponent<PlayerNetworkStats>();
            reviveHandler = GetComponent<PlayerReviveHandler>();
        }

        protected override void OnClientSpawned()
        {
            stats.IsDowned.OnValueChanged += OnIsDownedChanged;
            SetVisible(stats.IsDowned.Value);
        }

        protected override void OnClientDespawned()
        {
            stats.IsDowned.OnValueChanged -= OnIsDownedChanged;
            SetVisible(false);
        }

        private void OnIsDownedChanged(bool _, bool isDowned)
        {
            SetVisible(isDowned);
        }

        private void SetVisible(bool visible)
        {
            if (!visible)
            {
                if (rangeVisual != null) Destroy(rangeVisual);
                rangeVisual = null;
                return;
            }

            if (rangeVisual != null) return;

            rangeVisual = new GameObject("ReviveRangeVisual");
            AreaCircleVFX circle = rangeVisual.AddComponent<AreaCircleVFX>();
            circle.Initialize(reviveHandler.ReviveRadius, 0f, rangeColor, transform);
        }
    }
}
