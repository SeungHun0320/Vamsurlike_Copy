using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.Events;

namespace Vamsurlike.UI
{
    // 자신이 다운됐을 때, 팀원이 부활 중인 동안 진행도 바 표시.
    // ReviveProgressPayload.Progress < 0 → 취소(숨김), 0~1 → 진행 중(표시).
    public sealed class ReviveProgressUI : MonoBehaviour
    {
        [SerializeField] private GameObject      panel;
        [SerializeField] private Image           progressFill;
        [SerializeField] private TextMeshProUGUI progressText;

        private CanvasGroup cg;

        private void Awake()
        {
            if (panel == null) panel = gameObject;
            cg = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();
            if (progressFill != null) FilledImageUtility.ConfigureHorizontal(progressFill);
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (UIEventHub.Instance != null)
                UIEventHub.Instance.Player.ReviveProgressChanged += OnReviveProgress;
        }

        private void OnDisable()
        {
            if (UIEventHub.Instance != null)
                UIEventHub.Instance.Player.ReviveProgressChanged -= OnReviveProgress;
        }

        private void OnReviveProgress(ReviveProgressPayload p)
        {
            if (p.Progress < 0f) { SetVisible(false); return; }

            SetVisible(true);
            if (progressFill != null) progressFill.fillAmount = p.Progress;
            if (progressText != null) progressText.text = $"부활 중... {p.Progress * 100f:0}%";
        }

        private void SetVisible(bool visible)
        {
            cg.alpha          = visible ? 1f : 0f;
            cg.interactable   = visible;
            cg.blocksRaycasts = visible;
        }
    }
}
