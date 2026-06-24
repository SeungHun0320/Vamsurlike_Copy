using System.Collections.Generic;
using UnityEngine;

namespace Vamsurlike.Data
{
    // UE4 DataTable과 동일한 발상.
    // 테이블 1개 에셋이 전체 행을 보관. 인덱스 또는 ID로 조회.
    public abstract class DataTableSO<TRow> : ScriptableObject
        where TRow : struct
    {
        [SerializeField] private List<TRow> rows = new();

        public IReadOnlyList<TRow> Rows       => rows;
        public int                 Count      => rows.Count;
        public TRow                this[int i] => rows[i];

        public bool TryGet(int index, out TRow row)
        {
            if (index < 0 || index >= rows.Count) { row = default; return false; }
            row = rows[index];
            return true;
        }
    }
}
