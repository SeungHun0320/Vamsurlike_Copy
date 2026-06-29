using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.UI.Events;
using Vamsurlike.UI.ViewModels;

namespace Vamsurlike.UI
{
    public class SkillHUDUI : MonoBehaviour
    {
        [SerializeField] private Transform      container;
        [SerializeField] private SkillHUDCellUI cellPrefab;

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

        private void Render(SkillSlotsPayload payload)
        {
            if (container == null || cellPrefab == null) return;

            int count = payload.Names?.Length ?? 0;

            while (cells.Count < count)
                cells.Add(Instantiate(cellPrefab, container));

            for (int i = count; i < cells.Count; i++)
                cells[i].gameObject.SetActive(false);

            for (int i = 0; i < count; i++)
            {
                cells[i].gameObject.SetActive(true);
                cells[i].Set(payload.Names[i], payload.Levels[i]);
            }
        }
    }
}
