using System;
using System.Collections.Generic;
using System.Text;

namespace Jamkkaebi.Editor.TextTables
{
    /// <summary>
    /// RFC 4180 CSV 텍스트를 행·셀 목록으로 읽습니다. 에디터 임포트 전용이며 런타임에서 호출하지 않습니다.
    /// 헤더와 열의 의미는 다루지 않고 글자 단위 구문만 처리합니다. 열 개수 검사는 <see cref="TextTable"/>이 맡습니다.
    /// </summary>
    public static class CsvReader
    {
        private const char Quote = '"';
        private const char Separator = ',';
        private const char ByteOrderMark = '﻿';

        /// <summary>CSV 전체를 읽어 행 목록을 반환합니다.</summary>
        /// <param name="text">UTF-8로 읽은 파일 내용입니다. 맨 앞 BOM 한 글자는 무시합니다.</param>
        /// <returns>
        /// 각 행의 셀 목록입니다. 셀 안 줄바꿈은 <c>\n</c>으로 맞춰 값에 포함합니다.
        /// 빈 줄은 빈 셀 하나짜리 행으로 남기고, 파일 끝의 줄바꿈은 행을 추가하지 않습니다.
        /// </returns>
        /// <exception cref="ArgumentNullException">text가 null인 경우입니다.</exception>
        /// <exception cref="FormatException">
        /// 따옴표가 닫히지 않았거나, 닫는 따옴표 뒤에 구분자가 아닌 글자가 오거나, 따옴표 없는 셀 중간에 따옴표가 있는 경우입니다.
        /// 메시지에 CSV 행 번호(1부터)를 넣습니다.
        /// </exception>
        public static IReadOnlyList<IReadOnlyList<string>> Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>();
            List<string> row = new List<string>();
            StringBuilder cell = new StringBuilder();
            bool inQuotes = false;
            bool afterClosingQuote = false;
            // 마지막 줄바꿈 뒤에 남은 글자가 있는지 확인해, 파일 끝 줄바꿈이 빈 행을 만들지 않게 합니다.
            bool rowStarted = false;
            int index = text.Length > 0 && text[0] == ByteOrderMark ? 1 : 0;

            while (index < text.Length)
            {
                char current = text[index];
                if (inQuotes)
                {
                    if (current == Quote)
                    {
                        if (index + 1 < text.Length && text[index + 1] == Quote)
                        {
                            cell.Append(Quote);
                            index += 2;
                            continue;
                        }

                        inQuotes = false;
                        afterClosingQuote = true;
                    }
                    else if (current == '\r')
                    {
                        // 셀 안의 \r\n과 \r은 \n 하나로 맞춥니다.
                        cell.Append('\n');
                        if (index + 1 < text.Length && text[index + 1] == '\n')
                            index++;
                    }
                    else
                    {
                        cell.Append(current);
                    }

                    index++;
                    continue;
                }

                if (current == Separator)
                {
                    EndCell(row, cell);
                    afterClosingQuote = false;
                    rowStarted = true;
                }
                else if (current == '\r' || current == '\n')
                {
                    EndCell(row, cell);
                    rows.Add(row);
                    row = new List<string>();
                    afterClosingQuote = false;
                    rowStarted = false;
                    if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                        index++;
                }
                else if (afterClosingQuote)
                {
                    throw new FormatException($"{rows.Count + 1}행: 닫는 큰따옴표 뒤에는 쉼표나 줄바꿈이 와야 합니다.");
                }
                else if (current == Quote)
                {
                    if (cell.Length > 0)
                        throw new FormatException($"{rows.Count + 1}행: 따옴표로 감싸지 않은 셀 안에 큰따옴표가 있습니다.");
                    inQuotes = true;
                    rowStarted = true;
                }
                else
                {
                    cell.Append(current);
                    rowStarted = true;
                }

                index++;
            }

            if (inQuotes)
                throw new FormatException($"{rows.Count + 1}행: 큰따옴표가 닫히지 않았습니다.");
            if (rowStarted || afterClosingQuote)
            {
                EndCell(row, cell);
                rows.Add(row);
            }

            return rows;
        }

        /// <summary>모은 글자를 셀 하나로 행에 넣고 버퍼를 비웁니다.</summary>
        private static void EndCell(List<string> row, StringBuilder cell)
        {
            row.Add(cell.ToString());
            cell.Clear();
        }
    }
}
