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

        /// <summary>true면 셀 안 줄바꿈이 오류입니다. 화면에서 한 줄 자리에 들어가는 문구에 씁니다.</summary>
        public bool SingleLine { get; }

        /// <summary>0보다 크면 셀 글자 수(앞뒤 공백 제외)가 이 값을 넘을 때 오류입니다. 0이면 제한하지 않습니다.</summary>
        public int MaxLength { get; }

        /// <summary>열 정의를 만듭니다.</summary>
        /// <param name="name">헤더 이름입니다.</param>
        /// <param name="isRequired">헤더에 반드시 있어야 하는지 여부입니다.</param>
        /// <param name="allowEmpty">셀이 비어도 되는지 여부입니다.</param>
        /// <param name="singleLine">셀 안 줄바꿈을 금지할지 여부입니다.</param>
        /// <param name="maxLength">최대 글자 수이며 0이면 제한하지 않습니다.</param>
        /// <exception cref="ArgumentException">이름이 비어 있는 경우입니다.</exception>
        /// <exception cref="ArgumentOutOfRangeException">최대 글자 수가 음수인 경우입니다.</exception>
        public TextTableColumn(string name, bool isRequired, bool allowEmpty, bool singleLine = false, int maxLength = 0)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("열 이름이 필요합니다.", nameof(name));
            if (maxLength < 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), "최대 글자 수는 0 이상이어야 합니다.");
            Name = name;
            IsRequired = isRequired;
            AllowEmpty = allowEmpty;
            SingleLine = singleLine;
            MaxLength = maxLength;
        }

        /// <summary>헤더에 있어야 하고 값도 비면 안 되는 열입니다. 시트가 바꾸는 텍스트 열에 씁니다.</summary>
        public static TextTableColumn Value(string name) => new TextTableColumn(name, true, false);

        /// <summary>
        /// 헤더에 있어야 하고 값도 비면 안 되며, 줄바꿈 없이 maxLength 글자 이하여야 하는 열입니다.
        /// 카드 제목처럼 화면에서 높이가 고정된 한 줄 자리에 들어가는 문구에 씁니다.
        /// </summary>
        public static TextTableColumn SingleLineValue(string name, int maxLength) =>
            new TextTableColumn(name, true, false, true, maxLength);

        /// <summary>없어도 되고 비어도 되는 열입니다. 다른 원본이 기준인 값을 참고로 보여 주는 열에 씁니다.</summary>
        public static TextTableColumn Reference(string name) => new TextTableColumn(name, false, true);
    }
}
