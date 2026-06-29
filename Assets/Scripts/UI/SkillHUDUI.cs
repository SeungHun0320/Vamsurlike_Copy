using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    public class SkillHUDUI : MonoBehaviour
    {
        [SerializeField] private Transform      container;
        [SerializeField] private SkillHUDCellUI cellPrefab;
        [SerializeField] private Vector2        startOffset = new(34f, 34f);
        [SerializeField] private Vector2        cellSize = new(280f, 88f);
        [SerializeField] private float          cellSpacing = 10f;

        private readonly List<SkillHUDCellUI> cells = new();
        private SkillHUDViewModel viewModel;

        private void Awake()
        {
            viewModel = new SkillHUDViewModel();
        }

        private void OnEnable()
        {
            viewModel.OnSkillsChanged += Render;
            viewModel.Bind();
            Render(viewModel.Current); // 이미 수신된 상태 즉시 반영
        }

        private void OnDisable()
        {
            viewModel.OnSkillsChanged -= Render;
            viewModel.Unbind();
        }

        private void Render(IReadOnlyList<SkillHUDSlotViewData> slots)
        {
            if (container == null) container = transform;
            if (cellPrefab == null) return;

            int count = slots?.Count ?? 0;

            while (cells.Count < count)
                cells.Add(Instantiate(cellPrefab, container));

            for (int i = count; i < cells.Count; i++)
                cells[i].gameObject.SetActive(false);

            for (int i = 0; i < count; i++)
            {
                cells[i].gameObject.SetActive(true);
                ApplyCellLayout(cells[i], i);
                cells[i].Set(slots[i]);
            }
        }

        private void ApplyCellLayout(SkillHUDCellUI cell, int index)
        {
            if (cell == null) return;
            if (!cell.TryGetComponent(out RectTransform rect)) return;

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = cellSize;
            rect.anchoredPosition = new Vector2(-startOffset.x, startOffset.y)
                + Vector2.up * ((cellSize.y + cellSpacing) * index);
        }
    }
}
