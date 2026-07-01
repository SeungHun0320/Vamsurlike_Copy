using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vamsurlike.Network;
using Vamsurlike.Player;

namespace Vamsurlike.UI
{
    // 로비 플레이어 슬롯 1개 — 이름 + 색상 스와치.
    // Bind(state) 호출 시 NetworkVariable 변화에 자동 반응한다.
    public class LobbySlotUI : MonoBehaviour
    {
        [SerializeField] private GameObject      filledRoot;
        [SerializeField] private GameObject      emptyRoot;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image           colorSwatch;

        private LobbyPlayerState bound;

        public void Bind(LobbyPlayerState state)
        {
            Unbind();
            bound = state;
            state.DisplayName.OnValueChanged += OnNameChanged;
            state.ColorIndex.OnValueChanged  += OnColorChanged;
            Refresh();
        }

        public void Unbind()
        {
            if (bound == null) return;
            bound.DisplayName.OnValueChanged -= OnNameChanged;
            bound.ColorIndex.OnValueChanged  -= OnColorChanged;
            bound = null;
            ShowEmpty();
        }

        private void OnDestroy() => Unbind();

        private void OnNameChanged(FixedString64Bytes _, FixedString64Bytes __) => Refresh();
        private void OnColorChanged(int _, int __) => Refresh();

        private void Refresh()
        {
            if (bound == null) { ShowEmpty(); return; }

            if (emptyRoot  != null) emptyRoot.SetActive(false);
            if (filledRoot != null) filledRoot.SetActive(true);

            string name = bound.DisplayName.Value.ToString();
            if (string.IsNullOrWhiteSpace(name))
                name = $"Player {bound.OwnerClientId + 1}";
            if (nameText != null) nameText.text = name;

            int idx = Mathf.Clamp(bound.ColorIndex.Value, 0, PlayerColorSync.Palette.Length - 1);
            if (colorSwatch != null)
                colorSwatch.color = PlayerColorSync.Palette[idx];
        }

        private void ShowEmpty()
        {
            if (emptyRoot  != null) emptyRoot.SetActive(true);
            if (filledRoot != null) filledRoot.SetActive(false);
        }
    }
}
