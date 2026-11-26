using System.Linq;
using Jamkkaebi.Editor.TextTables;
using NUnit.Framework;

namespace Jamkkaebi.Tests.UI.EditMode
{
    public sealed class TextTableTests
    {
        private static readonly string[] Keys = { "a", "b" };

        private static readonly TextTableSchema Schema = new TextTableSchema("test.csv", "id",
            new[] { TextTableColumn.Value("text"), TextTableColumn.Reference("name") }, new[] { "비고" });

        private TextTableReport _report;

        [SetUp]
        public void SetUp() => _report = new TextTableReport();

        private TextTable Read(string csv) => TextTable.Read(csv, Schema, Keys, _report);

        /// <summary>정상 표는 키로 값을 찾고, 무시 열·빈 행·빠진 참고 열은 문제 삼지 않는지 검사합니다.</summary>
        [Test]
        public void Read_ValidTable_ReturnsRowsByKey()
        {
            TextTable table = Read("비고,id,text\n메모,b,둘\n,,\n,a, 하나 \n");

            Assert.That(table, Is.Not.Null, _report.Format());
            Assert.That(_report.Errors, Is.Empty);
            Assert.That(_report.Warnings, Is.Empty);
            Assert.That(table.Rows["a"].Get("text"), Is.EqualTo("하나"));
            Assert.That(table.Rows["a"].SheetRow, Is.EqualTo(4));
            Assert.That(table.Rows["b"].TryGet("name", out _), Is.False);
        }

        /// <summary>빠진 키를 오류로 보고하는지 검사합니다.</summary>
        [Test]
        public void Read_MissingKey_IsError()
        {
            Assert.That(Read("id,text\na,하나"), Is.Null);
            Assert.That(_report.Errors.Single(), Does.Contain("'b'"));
        }

        /// <summary>중복 키를 두 행 번호와 함께 오류로 보고하는지 검사합니다.</summary>
        [Test]
        public void Read_DuplicateKey_IsError()
        {
            Assert.That(Read("id,text\na,하나\nb,둘\na,또"), Is.Null);
            Assert.That(_report.Errors.Single(), Does.Contain("4행").And.Contain("2행"));
        }

        /// <summary>모르는 키를 오류로 보고하는지 검사합니다.</summary>
        [Test]
        public void Read_UnknownKey_IsError()
        {
            Assert.That(Read("id,text\na,하나\nb,둘\nc,셋"), Is.Null);
            Assert.That(_report.Errors.Single(), Does.Contain("'c'"));
        }

        /// <summary>필수 열·키 열이 없으면 행을 읽기 전에 오류로 멈추는지 검사합니다.</summary>
        [Test]
        public void Read_MissingRequiredColumn_IsError()
        {
            Assert.That(Read("id,name\na,x\nb,y"), Is.Null);
            Assert.That(_report.Errors.Single(), Does.Contain("'text'"));

            _report = new TextTableReport();
            Assert.That(Read("text\n하나"), Is.Null);
            Assert.That(_report.Errors.Single(), Does.Contain("'id'"));
        }

        /// <summary>비면 안 되는 값 셀과 빈 키를 오류로 보고하는지 검사합니다.</summary>
        [Test]
        public void Read_EmptyValueOrKey_IsError()
        {
            Assert.That(Read("id,text,name\na,,x\nb,둘,\n,셋,"), Is.Null);
            Assert.That(_report.Errors.Count, Is.EqualTo(2), _report.Format());
            Assert.That(_report.Errors[0], Does.Contain("2행").And.Contain("'text'"));
            Assert.That(_report.Errors[1], Does.Contain("4행").And.Contain("'id'"));
        }

        /// <summary>헤더와 셀 개수가 다른 행과 중복 헤더를 오류로 보고하는지 검사합니다.</summary>
        [Test]
        public void Read_MalformedShape_IsError()
        {
            Assert.That(Read("id,text\na,하나,덤\nb,둘"), Is.Null);
            Assert.That(_report.Format(), Does.Contain("셀이 3개"));

            _report = new TextTableReport();
            Assert.That(Read("id,text,text\na,1,2\nb,1,2"), Is.Null);
            Assert.That(_report.Format(), Does.Contain("두 번"));
        }

        /// <summary>스키마에 없는 열은 경고만 하고 표는 읽는지 검사합니다.</summary>
        [Test]
        public void Read_UnknownColumn_IsWarning()
        {
            Assert.That(Read("id,text,설명 \na,하나,x\nb,둘,y"), Is.Not.Null);
            Assert.That(_report.Warnings.Single(), Does.Contain("'설명'"));
        }

        /// <summary>CSV 구문 오류를 예외 대신 보고서 오류로 바꾸는지 검사합니다.</summary>
        [Test]
        public void Read_CsvSyntaxError_IsReported()
        {
            Assert.That(Read("id,text\na,\"하나"), Is.Null);
            Assert.That(_report.Errors.Single(), Does.Contain("CSV 형식 오류"));
        }
    }
}
