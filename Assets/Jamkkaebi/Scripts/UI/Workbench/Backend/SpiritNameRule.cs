namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>
    /// 정령 이름 규칙. 서버 SpiritNamePolicy와 같다(금칙어는 서버만 검사한다).
    /// 모의 백엔드와 이름 입력 화면이 함께 쓴다.
    /// </summary>
    public static class SpiritNameRule
    {
        public const int MinLength = 2;
        public const int MaxLength = 10;

        /// <summary>입력을 저장할 이름으로 바꾼다.</summary>
        /// <param name="input">사용자가 입력한 이름.</param>
        /// <param name="normalized">저장할 이름. 앞뒤 공백을 자른 값이며, 기본 이름으로 되돌릴 때는 null.</param>
        /// <param name="error">규칙에 어긋날 때 사용자에게 보여 줄 문장. 통과하면 null.</param>
        /// <returns>규칙을 통과했는지.</returns>
        public static bool TryNormalize(string input, out string normalized, out string error)
        {
            normalized = null;
            error = null;

            // 비우면 기본 이름으로 되돌린다는 뜻이다(서버와 같음).
            if (string.IsNullOrWhiteSpace(input)) return true;

            string trimmed = input.Trim();
            int length = CountCodePoints(trimmed);
            if (length < MinLength)
            {
                error = $"이름은 {MinLength}자 이상이어야 해요.";
                return false;
            }
            if (length > MaxLength)
            {
                error = $"이름은 {MaxLength}자를 넘을 수 없어요.";
                return false;
            }

            normalized = trimmed;
            return true;
        }

        /// <summary>
        /// 글자 수. string.Length는 UTF-16 단위라 이모지 같은 글자를 2로 센다.
        /// 서버(Java codePointCount)처럼 서로게이트 쌍을 한 글자로 센다.
        /// </summary>
        public static int CountCodePoints(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
                count++;
            }
            return count;
        }
    }
}
