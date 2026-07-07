using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI
{
    // Co-op HUD — 팀원 HP 미니 슬롯 표시.
    // LocalClientId 제외. PlayerStatusChanged 이벤트 수신 시 슬롯 프리팹 Instantiate.
    public sealed class CoopHUDUI : MonoBehaviour
    {
        [Header("Slot Prefab")]
        [SerializeField] private PlayerSlotUI slotPrefab;

        [Header("Layout")]
        [SerializeField] private float slotHeight  = 88f;
        [SerializeField] private float slotSpacing = 8f;

        [Header("Colors")]
        [SerializeField] private Color aliveColor  = new Color(0.2f, 0.8f, 0.2f,  1f);
        [SerializeField] private Color downedColor = new Color(0.9f, 0.3f, 0.1f,  1f);
        [SerializeField] private Color deadColor   = new Color(0.4f, 0.4f, 0.4f, 0.6f);

        private readonly Dictionary<ulong, PlayerSlotUI> slots = new();

        private void OnEnable()
        {
            if (UIEventHub.Instance != null)
                UIEventHub.Instance.Player.PlayerStatusChanged += OnPlayerStatus;
        }

        private void OnDisable()
        {
            if (UIEventHub.Instance != null)
                UIEventHub.Instance.Player.PlayerStatusChanged -= OnPlayerStatus;
        }

        private void OnPlayerStatus(PlayerStatusPayload p)
        {
            if (NetworkManager.Singleton != null
                && p.ClientId == NetworkManager.Singleton.LocalClientId)
                return;

            if (!slots.TryGetValue(p.ClientId, out var slot))
            {
                slot = CreateSlot(p.ClientId, p.DisplayName);
                if (slot == null) return;

                slots[p.ClientId] = slot;
                RebuildLayout();
            }

            slot.UpdateSlot(p, aliveColor, downedColor, deadColor);
        }

        private PlayerSlotUI CreateSlot(ulong clientId, string displayName)
        {
            if (slotPrefab == null)
            {
                Debug.LogError("[CoopHUDUI] slotPrefab이 할당되지 않았습니다.", this);
                return null;
            }

            var slot = Instantiate(slotPrefab, transform, false);
            slot.name = $"Slot_{clientId}";

            var rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, slotHeight);

            if (slot.nameText != null)
                slot.nameText.text = string.IsNullOrEmpty(displayName) ? $"P{clientId}" : displayName;

            return slot;
        }

        private void RebuildLayout()
        {
            int i = 0;
            foreach (var kvp in slots)
            {
                if (kvp.Value == null) continue;
                var rt = kvp.Value.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0f, -(i * (slotHeight + slotSpacing)));
                i++;
            }
        }
    }
}
