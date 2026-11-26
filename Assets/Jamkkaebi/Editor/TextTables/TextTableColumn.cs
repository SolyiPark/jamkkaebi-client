using System;

namespace Jamkkaebi.Editor.TextTables
{
    /// <summary>텍스트 표 스키마의 열 하나입니다. 헤더 이름으로 찾습니다.</summary>
    public sealed class TextTableColumn
    {
        /// <summary>헤더 행에 적힌 열 이름입니다. 대소문자와 공백을 구분합니다.</summary>
        public string Name { get; }

        /// <summary>true면 헤더에 이 열이 없을 때 오류입니다. false인 열(참고 열 등)은 없어도 됩니다.</summary>
        public bool IsRequired { get; }

        /// <summary>false면 키가 있는 행에서 이 열의 셀이 비어 있을 때 오류입니다.</summary>
        public bool AllowEmpty { get; }

        /// <summary>열 정의를 만듭니다.</summary>
        /// <param name="name">헤더 이름입니다.</param>
        /// <param name="isRequired">헤더에 반드시 있어야 하는지 여부입니다.</param>
        /// <param name="allowEmpty">셀이 비어도 되는지 여부입니다.</param>
        /// <exception cref="ArgumentException">이름이 비어 있는 경우입니다.</exception>
        public TextTableColumn(string name, bool isRequired, bool allowEmpty)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("열 이름이 필요합니다.", nameof(name));
            Name = name;
            IsRequired = isRequired;
            AllowEmpty = allowEmpty;
        }

        /// <summary>헤더에 있어야 하고 값도 비면 안 되는 열입니다. 시트가 바꾸는 텍스트 열에 씁니다.</summary>
        public static TextTableColumn Value(string name) => new TextTableColumn(name, true, false);

        /// <summary>없어도 되고 비어도 되는 열입니다. 다른 원본이 기준인 값을 참고로 보여 주는 열에 씁니다.</summary>
        public static TextTableColumn Reference(string name) => new TextTableColumn(name, false, true);
    }
}
