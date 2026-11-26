using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Editor.TextTables;
using NUnit.Framework;

namespace Jamkkaebi.Tests.UI.EditMode
{
    public sealed class CsvReaderTests
    {
        private static string[][] Parse(string text) =>
            CsvReader.Parse(text).Select(row => row.ToArray()).ToArray();

        /// <summary>따옴표 없는 셀을 쉼표로 나누는지 검사합니다.</summary>
        [Test]
        public void Parse_PlainCells_SplitsByComma()
        {
            Assert.That(Parse("a,b,c\nd,e,f"), Is.EqualTo(new[]
            {
                new[] { "a", "b", "c" }, new[] { "d", "e", "f" }
            }));
        }

        /// <summary>따옴표 안의 쉼표는 셀을 나누지 않는지 검사합니다.</summary>
        [Test]
        public void Parse_QuotedComma_StaysInCell()
        {
            Assert.That(Parse("\"a,b\",c"), Is.EqualTo(new[] { new[] { "a,b", "c" } }));
        }

        /// <summary>따옴표 안의 줄바꿈은 행을 나누지 않고, \r\n도 \n으로 맞추는지 검사합니다.</summary>
        [Test]
        public void Parse_QuotedNewline_StaysInCell()
        {
            Assert.That(Parse("\"첫 줄\n둘째 줄\",x\r\n\"a\r\nb\",y"), Is.EqualTo(new[]
            {
                new[] { "첫 줄\n둘째 줄", "x" }, new[] { "a\nb", "y" }
            }));
        }

        /// <summary>따옴표 안의 연속 큰따옴표 두 개가 하나로 읽히는지 검사합니다.</summary>
        [Test]
        public void Parse_EscapedQuote_BecomesSingleQuote()
        {
            Assert.That(Parse("\"a \"\"b\"\"\",\"\"\"\""), Is.EqualTo(new[] { new[] { "a \"b\"", "\"" } }));
        }

        /// <summary>맨 앞 UTF-8 BOM을 첫 셀에 넣지 않는지 검사합니다.</summary>
        [Test]
        public void Parse_LeadingBom_IsIgnored()
        {
            Assert.That(Parse("﻿id,name\nx,y")[0][0], Is.EqualTo("id"));
        }

        /// <summary>중간·끝의 빈 셀과 따옴표로 감싼 빈 셀이 빈 문자열인지 검사합니다.</summary>
        [Test]
        public void Parse_EmptyCells_AreEmptyStrings()
        {
            Assert.That(Parse("a,,b\na,\n\"\",x"), Is.EqualTo(new[]
            {
                new[] { "a", "", "b" }, new[] { "a", "" }, new[] { "", "x" }
            }));
        }

        /// <summary>CRLF·LF 줄바꿈이 같은 행을 만드는지 검사합니다.</summary>
        [Test]
        public void Parse_CrLfAndLf_GiveSameRows()
        {
            Assert.That(Parse("a,b\r\nc,d\r\n"), Is.EqualTo(Parse("a,b\nc,d\n")));
        }

        /// <summary>파일 끝 줄바꿈이 빈 행을 더하지 않고, 중간 빈 줄은 빈 셀 하나짜리 행으로 남는지 검사합니다.</summary>
        [Test]
        public void Parse_TrailingNewline_DoesNotAddEmptyRow()
        {
            Assert.That(Parse("a\n"), Is.EqualTo(new[] { new[] { "a" } }));
            Assert.That(Parse("a\n\nb"), Is.EqualTo(new[] { new[] { "a" }, new[] { "" }, new[] { "b" } }));
            Assert.That(Parse(""), Is.Empty);
        }

        /// <summary>닫히지 않은 따옴표를 행 번호와 함께 거부하는지 검사합니다.</summary>
        [Test]
        public void Parse_UnclosedQuote_Throws()
        {
            FormatException exception = Assert.Throws<FormatException>(() => CsvReader.Parse("a,b\n\"c,d\ne"));
            StringAssert.Contains("2행", exception.Message);
        }

        /// <summary>닫는 따옴표 뒤 글자와 따옴표 없는 셀 중간의 따옴표를 거부하는지 검사합니다.</summary>
        [Test]
        public void Parse_StrayQuote_Throws()
        {
            Assert.Throws<FormatException>(() => CsvReader.Parse("\"a\"b,c"));
            Assert.Throws<FormatException>(() => CsvReader.Parse("ab\"c\",d"));
        }

        /// <summary>null 입력을 거부하는지 검사합니다.</summary>
        [Test]
        public void Parse_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CsvReader.Parse(null));
        }
    }
}
