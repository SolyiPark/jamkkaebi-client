using System;
using System.Collections.Generic;

namespace Jamkkaebi.Editor.TextTables
{
    /// <summary>
    /// 텍스트 표 하나의 열 구성입니다. 첫 행은 헤더, 키 열은 행 식별자이며,
    /// 무시할 열(비고 등)과 이름이 비어 있는 열은 읽지 않습니다. 그 밖의 모르는 열은 경고합니다.
    /// </summary>
    public sealed class TextTableSchema
    {
        private readonly List<TextTableColumn> _columns;
        private readonly HashSet<string> _ignoredColumns;

        /// <summary>보고 메시지에 쓰는 표 이름(보통 CSV 파일 이름)입니다.</summary>
        public string TableName { get; }

        /// <summary>행을 식별하는 키 열 이름입니다. 항상 필수이며 값이 비면 안 됩니다.</summary>
        public string KeyColumn { get; }

        /// <summary>키 열을 뺀 값·참고 열 목록입니다.</summary>
        public IReadOnlyList<TextTableColumn> Columns => _columns;

        /// <summary>스키마를 만듭니다.</summary>
        /// <param name="tableName">보고에 쓸 표 이름입니다.</param>
        /// <param name="keyColumn">키 열 이름입니다.</param>
        /// <param name="columns">키 열을 뺀 열 정의입니다.</param>
        /// <param name="ignoredColumns">읽지 않고 경고도 하지 않을 열 이름입니다.</param>
        /// <exception cref="ArgumentException">이름이 비었거나 열 이름이 겹치는 경우입니다.</exception>
        /// <exception cref="ArgumentNullException">열 목록이 null인 경우입니다.</exception>
        public TextTableSchema(string tableName, string keyColumn, IEnumerable<TextTableColumn> columns,
            IEnumerable<string> ignoredColumns = null)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("표 이름이 필요합니다.", nameof(tableName));
            if (string.IsNullOrWhiteSpace(keyColumn))
                throw new ArgumentException("키 열 이름이 필요합니다.", nameof(keyColumn));
            if (columns == null)
                throw new ArgumentNullException(nameof(columns));

            TableName = tableName;
            KeyColumn = keyColumn;
            _columns = new List<TextTableColumn>();
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal) { keyColumn };
            foreach (TextTableColumn column in columns)
            {
                if (column == null || !names.Add(column.Name))
                    throw new ArgumentException("열 이름은 키 열과 겹치지 않고 서로 달라야 합니다.", nameof(columns));
                _columns.Add(column);
            }

            _ignoredColumns = new HashSet<string>(ignoredColumns ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (_ignoredColumns.Overlaps(names))
                throw new ArgumentException("무시할 열은 스키마 열과 겹칠 수 없습니다.", nameof(ignoredColumns));
        }

        /// <summary>읽지 않고 경고도 하지 않는 열인지 반환합니다.</summary>
        public bool IsIgnored(string header) => string.IsNullOrWhiteSpace(header) || _ignoredColumns.Contains(header);

        /// <summary>이름으로 스키마 열을 찾습니다. 키 열과 모르는 이름은 null입니다.</summary>
        public TextTableColumn FindColumn(string name)
        {
            foreach (TextTableColumn column in _columns)
            {
                if (string.Equals(column.Name, name, StringComparison.Ordinal))
                    return column;
            }

            return null;
        }
    }
}
