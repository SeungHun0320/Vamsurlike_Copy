using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI
{
    // Co-op HUD — 팀원 HP 미니 슬롯 표시.
    // LocalClientId 제외. PlayerStatusChanged 이벤트 수신 시 슬롯 자동 생성/갱신.
    public sealed class CoopHUDUI : MonoBehaviour
    {
        [SerializeField] private float slotHeight  = 52f;
        [SerializeField] private float slotSpacing = 6f;
        [SerializeField] private Color aliveColor  = new Color(0.2f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color downedColor = new Color(0.9f, 0.3f, 0.1f, 1f);
        [SerializeField] private Color deadColor   = new Color(0.4f, 0.4f, 0.4f, 0.6f);

        private readonly Dictionary<ulong, SlotRefs> slots = new();

        private sealed class SlotRefs
        {
            public GameObject      root;
            public Image           hpFill;
            public TextMeshProUGUI nameText;
            public GameObject      downedIcon;
        }

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
            if (NetworkManager.Singleton != null && p.ClientId == NetworkManager.Singleton.LocalClientId)
                return;

            if (!slots.TryGetValue(p.ClientId, out var slot))
            {
                slot = BuildSlot(p.ClientId, p.DisplayName);
                slots[p.ClientId] = slot;
                RebuildLayout();
            }

            if (slot.hpFill != null)
            {
                float normalized = p.MaxHp > 0f ? p.Hp / p.MaxHp : 0f;
                slot.hpFill.fillAmount = Mathf.Clamp01(normalized);
                slot.hpFill.color = p.IsDowned ? downedColor : p.IsAlive ? aliveColor : deadColor;
            }

            if (slot.nameText != null)
                slot.nameText.text = string.IsNullOrEmpty(p.DisplayName) ? $"P{p.ClientId}" : p.DisplayName;

            if (slot.downedIcon != null)
                slot.downedIcon.SetActive(p.IsDowned);
        }

        private SlotRefs BuildSlot(ulong clientId, string displayName)
        {
            var root = new GameObject($"Slot_{clientId}");
            root.transform.SetParent(transform, false);

            root.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.8f);

            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, slotHeight);

            // HP fill
            var fillGO = new GameObject("HPFill");
            fillGO.transform.SetParent(root.transform, false);
            var fill = fillGO.AddComponent<Image>();
            fill.color = aliveColor;
            FilledImageUtility.ConfigureHorizontal(fill);
            var fillRt = fillGO.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;

            // 이름 텍스트
            var nameGO = new GameObject("Name");
            nameGO.transform.SetParent(root.transform, false);
            var nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.text      = string.IsNullOrEmpty(displayName) ? $"P{clientId}" : displayName;
            nameText.fontSize  = 18f;
            nameText.color     = Color.white;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            var nameRt = nameGO.GetComponent<RectTransform>();
            nameRt.anchorMin        = Vector2.zero;
            nameRt.anchorMax        = Vector2.one;
            nameRt.sizeDelta        = new Vector2(-8f, 0f);
            nameRt.anchoredPosition = new Vector2(6f,  0f);

            // 다운 오버레이
            var downGO = new GameObject("DownedOverlay");
            downGO.transform.SetParent(root.transform, false);
            downGO.AddComponent<Image>().color = new Color(0.8f, 0.1f, 0.05f, 0.65f);
            var downRt = downGO.GetComponent<RectTransform>();
            downRt.anchorMin = Vector2.zero;
            downRt.anchorMax = Vector2.one;
            downRt.sizeDelta = Vector2.zero;

            var downTxtGO = new GameObject("Label");
            downTxtGO.transform.SetParent(downGO.transform, false);
            var downTxt = downTxtGO.AddComponent<TextMeshProUGUI>();
            downTxt.text      = "다운됨";
            downTxt.fontSize  = 16f;
            downTxt.color     = Color.white;
            downTxt.fontStyle = FontStyles.Bold;
            downTxt.alignment = TextAlignmentOptions.Center;
            var downTxtRt = downTxtGO.GetComponent<RectTransform>();
            downTxtRt.anchorMin = Vector2.zero;
            downTxtRt.anchorMax = Vector2.one;
            downTxtRt.sizeDelta = Vector2.zero;

            downGO.SetActive(false);

            return new SlotRefs { root = root, hpFill = fill, nameText = nameText, downedIcon = downGO };
        }

        private void RebuildLayout()
        {
            int i = 0;
            foreach (var kvp in slots)
            {
                var slotRt = kvp.Value.root.GetComponent<RectTransform>();
                slotRt.anchoredPosition = new Vector2(0f, -(i * (slotHeight + slotSpacing)));
                i++;
            }
        }
    }
}
