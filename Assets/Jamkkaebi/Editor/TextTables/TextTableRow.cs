using System;
using System.Collections.Generic;

namespace Jamkkaebi.Editor.TextTables
{
    /// <summary>검증을 통과한 표의 한 행입니다. 셀 값은 앞뒤 공백을 지운 상태입니다.</summary>
    public sealed class TextTableRow
    {
        private readonly Dictionary<string, string> _cells;

        /// <summary>키 열의 값입니다.</summary>
        public string Key { get; }

        /// <summary>시트에서 찾을 때 쓰는 행 번호입니다(헤더가 1행).</summary>
        public int SheetRow { get; }

        internal TextTableRow(string key, int sheetRow, Dictionary<string, string> cells)
        {
            Key = key;
            SheetRow = sheetRow;
            _cells = cells;
        }

        /// <summary>열 값을 찾습니다. 헤더에 없던 열(빠진 참고 열 등)은 false입니다.</summary>
        public bool TryGet(string column, out string value) => _cells.TryGetValue(column, out value);

        /// <summary>필수 열 값을 반환합니다.</summary>
        /// <exception cref="KeyNotFoundException">헤더에 없던 열인 경우입니다.</exception>
        public string Get(string column)
        {
            if (!_cells.TryGetValue(column, out string value))
                throw new KeyNotFoundException($"'{column}' 열이 표에 없습니다.");
            return value;
        }
    }
}
