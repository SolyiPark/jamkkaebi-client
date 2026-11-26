using System;
using System.Collections.Generic;

namespace Jamkkaebi.Editor.TextTables
{
    /// <summary>
    /// 헤더가 있는 CSV를 스키마와 기대 키 목록으로 검증한 결과입니다.
    /// 행 순서는 의미가 없으며 키로 찾습니다. 모든 셀이 빈 행은 건너뜁니다.
    /// </summary>
    public sealed class TextTable
    {
        private readonly Dictionary<string, TextTableRow> _rows;

        public TextTableSchema Schema { get; }

        /// <summary>키별 행입니다.</summary>
        public IReadOnlyDictionary<string, TextTableRow> Rows => _rows;

        private TextTable(TextTableSchema schema, Dictionary<string, TextTableRow> rows)
        {
            Schema = schema;
            _rows = rows;
        }

        /// <summary>
        /// CSV를 읽고 검증합니다. 다음은 오류입니다: CSV 구문 오류, 헤더 없음, 헤더 중복, 필수 열 없음,
        /// 헤더와 셀 개수가 다른 행, 키가 빈 행, 중복 키, 기대 목록에 없는 키, 기대 목록에서 빠진 키, 비면 안 되는 셀이 빈 경우.
        /// 스키마에 없고 무시 목록에도 없는 열은 경고입니다.
        /// </summary>
        /// <param name="csvText">CSV 파일 내용입니다.</param>
        /// <param name="schema">열 구성입니다.</param>
        /// <param name="expectedKeys">표가 정확히 한 번씩 덮어야 하는 키입니다.</param>
        /// <param name="report">오류·경고를 추가할 보고서입니다.</param>
        /// <returns>오류가 없으면 표, 이번 호출에서 오류가 하나라도 생기면 null입니다.</returns>
        /// <exception cref="ArgumentNullException">인자 중 하나가 null인 경우입니다.</exception>
        public static TextTable Read(string csvText, TextTableSchema schema, IEnumerable<string> expectedKeys,
            TextTableReport report)
        {
            if (csvText == null)
                throw new ArgumentNullException(nameof(csvText));
            if (schema == null)
                throw new ArgumentNullException(nameof(schema));
            if (expectedKeys == null)
                throw new ArgumentNullException(nameof(expectedKeys));
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            int errorCount = report.Errors.Count;
            string table = schema.TableName;
            IReadOnlyList<IReadOnlyList<string>> records;
            try
            {
                records = CsvReader.Parse(csvText);
            }
            catch (FormatException exception)
            {
                report.AddError($"{table}: CSV 형식 오류 - {exception.Message}");
                return null;
            }

            if (records.Count == 0)
            {
                report.AddError($"{table}: 헤더 행이 없습니다.");
                return null;
            }

            // 헤더 이름 → 열 위치. 무시할 열은 넣지 않습니다.
            IReadOnlyList<string> header = records[0];
            Dictionary<string, int> columnIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < header.Count; i++)
            {
                string name = header[i].Trim();
                if (schema.IsIgnored(name))
                    continue;
                if (columnIndex.ContainsKey(name))
                {
                    report.AddError($"{table}: 헤더 '{name}'이(가) 두 번 있습니다.");
                    continue;
                }

                columnIndex.Add(name, i);
                if (name != schema.KeyColumn && schema.FindColumn(name) == null)
                    report.AddWarning($"{table}: 모르는 열 '{name}'은(는) 읽지 않습니다. 이름이 바뀌었는지 확인하세요.");
            }

            if (!columnIndex.ContainsKey(schema.KeyColumn))
                report.AddError($"{table}: 키 열 '{schema.KeyColumn}'이(가) 없습니다.");
            foreach (TextTableColumn column in schema.Columns)
            {
                if (column.IsRequired && !columnIndex.ContainsKey(column.Name))
                    report.AddError($"{table}: 필수 열 '{column.Name}'이(가) 없습니다.");
            }

            if (report.Errors.Count > errorCount)
                return null;

            HashSet<string> expected = new HashSet<string>(expectedKeys, StringComparer.Ordinal);
            Dictionary<string, TextTableRow> rows = new Dictionary<string, TextTableRow>(StringComparer.Ordinal);
            for (int r = 1; r < records.Count; r++)
            {
                IReadOnlyList<string> record = records[r];
                int sheetRow = r + 1;
                if (IsBlank(record))
                    continue;
                if (record.Count != header.Count)
                {
                    report.AddError($"{table} {sheetRow}행: 셀이 {record.Count}개입니다. 헤더는 {header.Count}개입니다.");
                    continue;
                }

                string key = record[columnIndex[schema.KeyColumn]].Trim();
                if (key.Length == 0)
                {
                    report.AddError($"{table} {sheetRow}행: '{schema.KeyColumn}'이(가) 비어 있습니다.");
                    continue;
                }
                if (!expected.Contains(key))
                {
                    report.AddError($"{table} {sheetRow}행: 모르는 ID '{key}'입니다.");
                    continue;
                }
                if (rows.TryGetValue(key, out TextTableRow first))
                {
                    report.AddError($"{table} {sheetRow}행: ID '{key}'이(가) {first.SheetRow}행과 겹칩니다.");
                    continue;
                }

                Dictionary<string, string> cells = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (TextTableColumn column in schema.Columns)
                {
                    if (!columnIndex.TryGetValue(column.Name, out int index))
                        continue;
                    string value = record[index].Trim();
                    if (value.Length == 0 && !column.AllowEmpty)
                        report.AddError($"{table} {sheetRow}행 ({key}): '{column.Name}'이(가) 비어 있습니다.");
                    cells.Add(column.Name, value);
                }

                rows.Add(key, new TextTableRow(key, sheetRow, cells));
            }

            foreach (string key in expectedKeys)
            {
                if (!rows.ContainsKey(key))
                    report.AddError($"{table}: ID '{key}'의 행이 없습니다.");
            }

            return report.Errors.Count > errorCount ? null : new TextTable(schema, rows);
        }

        /// <summary>모든 셀이 공백뿐인 행인지 반환합니다. 시트 끝의 빈 행을 건너뛸 때 씁니다.</summary>
        private static bool IsBlank(IReadOnlyList<string> record)
        {
            foreach (string cell in record)
            {
                if (!string.IsNullOrWhiteSpace(cell))
                    return false;
            }

            return true;
        }
    }
}
