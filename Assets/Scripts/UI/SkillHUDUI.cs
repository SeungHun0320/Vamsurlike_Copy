using System.Collections.Generic;
using UnityEngine;
using Vamsurlike.Skills;

namespace Vamsurlike.UI
{
    // 플레이 화면 오른쪽 — 현재 보유 스킬과 레벨을 세로 목록으로 표시.
    // 설정:
    //   container   : Vertical Layout Group이 붙은 빈 RectTransform
    //   cellPrefab  : SkillHUDCellUI가 붙은 프리팹 (작은 카드 형태)
    public class SkillHUDUI : MonoBehaviour
    {
        [SerializeField] private Transform       container;
        [SerializeField] private SkillHUDCellUI  cellPrefab;

        private readonly List<SkillHUDCellUI> cells = new();

        private void OnEnable()  => SkillManager.OnSkillsSynced += Refresh;
        private void OnDisable() => SkillManager.OnSkillsSynced -= Refresh;

        private void Refresh(string[] names, int[] levels)
        {
            if (container == null || cellPrefab == null) return;

            int count = names.Length;

            // 필요한 셀 추가
            while (cells.Count < count)
                cells.Add(Instantiate(cellPrefab, container));

            // 초과 셀 숨기기
            for (int i = count; i < cells.Count; i++)
                cells[i].gameObject.SetActive(false);

            // 셀 업데이트
            for (int i = 0; i < count; i++)
            {
                cells[i].gameObject.SetActive(true);
                cells[i].Set(names[i], levels[i]);
            }
        }
    }
}
