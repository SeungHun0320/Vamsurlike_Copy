using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    // PlayerResultRow 프리팹에 배치.
    // 헤더 버튼 클릭 시 스킬 목록 펼치기/접기.
    public class PlayerResultRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text  playerNameText;
        [SerializeField] private TMP_Text  statsText;
        [SerializeField] private Transform barContainer;
        [SerializeField] private StatBarRowUI barRowPrefab;
        [SerializeField] private Button    expandButton;
        [SerializeField] private TMP_Text  expandIcon;
        [SerializeField] private GameObject skillList;
        [SerializeField] private Transform skillListContent;
        [SerializeField] private PlayerSkillRowUI skillRowPrefab;

        [Header("Layout")]
        [SerializeField] private float collapsedHeight = 48f;
        [SerializeField] private float barRowHeight = 20f;
        [SerializeField] private float barRowSpacing = 2f;
        [SerializeField] private float skillRowHeight = 28f;
        [SerializeField] private float skillRowSpacing = 2f;
        [SerializeField] private int rebuildFrames = 2;

        private RectTransform rowRect;
        private RectTransform skillListRect;
        private RectTransform skillListContentRect;
        private RectTransform barContainerRect;
        private LayoutElement rowLayout;
        private Coroutine rebuildCoroutine;
        private PlayerResultViewModel currentVm;

        private void Awake()
        {
            rowRect = transform as RectTransform;
            skillListRect = skillList != null ? skillList.transform as RectTransform : null;
            skillListContentRect = skillListContent as RectTransform;
            barContainerRect = barContainer as RectTransform;
            rowLayout = GetComponent<LayoutElement>();
            if (rowLayout == null)
                rowLayout = gameObject.AddComponent<LayoutElement>();

            if (expandButton != null)
                expandButton.onClick.AddListener(ToggleExpand);
            RenderState();
        }

        private void OnDestroy()
        {
            if (expandButton != null)
                expandButton.onClick.RemoveListener(ToggleExpand);
            UnbindViewModel();
        }

        public void Bind(PlayerResultViewModel vm)
        {
            if (vm == null) return;
            UnbindViewModel();
            currentVm = vm;
            currentVm.Changed += RenderState;

            if (playerNameText != null) playerNameText.text = vm.PlayerName;
            if (statsText      != null) statsText.text      = vm.StatsSummary;

            if (barContainer != null)
            {
                foreach (Transform child in barContainer)
                    Destroy(child.gameObject);

                if (barRowPrefab != null && vm.Bars != null)
                {
                    foreach (var bar in vm.Bars)
                    {
                        var row = Instantiate(barRowPrefab, barContainer);
                        row.Bind(bar);
                    }
                }
            }

            SkillResultViewModel[] skills = vm.Skills;

            if (skillListContent != null)
            {
                foreach (Transform child in skillListContent)
                    Destroy(child.gameObject);

                if (skillRowPrefab != null && skills != null)
                {
                    foreach (var skill in skills)
                    {
                        var row = Instantiate(skillRowPrefab, skillListContent);
                        row.Bind(skill);
                    }
                }
            }

            // 스킬이 없으면 확장 버튼 비활성화
            if (expandButton != null)
                expandButton.gameObject.SetActive(vm.CanExpand);

            vm.SetExpanded(false);
            RenderState();
        }

        private void ToggleExpand()
        {
            currentVm?.ToggleExpanded();
        }

        private void RenderState()
        {
            bool expanded = currentVm != null && currentVm.IsExpanded;
            if (skillList  != null) skillList.SetActive(expanded);
            if (expandIcon != null) expandIcon.text = expanded ? "▼" : "▶";

            ApplyPreferredHeight();
            RebuildLayouts();

            if (!gameObject.activeInHierarchy) return;
            if (rebuildCoroutine != null) StopCoroutine(rebuildCoroutine);
            rebuildCoroutine = StartCoroutine(RebuildLayoutsForFrames());
        }

        private void ApplyPreferredHeight()
        {
            float height = collapsedHeight;

            // 막대 그래프는 접이식이 아니라 항상 보이므로 기본 높이에 매번 더한다.
            int barCount = currentVm?.Bars?.Length ?? 0;
            if (barCount > 0)
                height += barCount * barRowHeight + Mathf.Max(0, barCount - 1) * barRowSpacing;

            int skillCount = currentVm?.SkillCount ?? 0;
            if (currentVm != null && currentVm.IsExpanded)
                height += skillCount * skillRowHeight + Mathf.Max(0, skillCount - 1) * skillRowSpacing;

            if (rowLayout != null)
            {
                rowLayout.minHeight = height;
                rowLayout.preferredHeight = height;
            }

            if (rowRect != null)
                rowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private IEnumerator RebuildLayoutsForFrames()
        {
            int frames = Mathf.Max(1, rebuildFrames);
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                RebuildLayouts();
            }

            rebuildCoroutine = null;
        }

        private void RebuildLayouts()
        {
            Canvas.ForceUpdateCanvases();
            ForceRebuild(barContainerRect);
            ForceRebuild(skillListContentRect);
            ForceRebuild(skillListRect);
            ForceRebuild(rowRect);

            Transform current = transform.parent;
            while (current != null)
            {
                ForceRebuild(current as RectTransform);
                current = current.parent;
            }
        }

        private static void ForceRebuild(RectTransform rect)
        {
            if (rect == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        private void UnbindViewModel()
        {
            if (currentVm == null) return;
            currentVm.Changed -= RenderState;
            currentVm = null;
        }
    }
}
