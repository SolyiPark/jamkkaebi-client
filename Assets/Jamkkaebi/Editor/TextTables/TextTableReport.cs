using System.Collections.Generic;
using System.Text;

namespace Jamkkaebi.Editor.TextTables
{
    /// <summary>텍스트 표 임포트 중 모은 오류와 경고입니다. 오류가 하나라도 있으면 임포트하지 않습니다.</summary>
    public sealed class TextTableReport
    {
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        public IReadOnlyList<string> Errors => _errors;
        public IReadOnlyList<string> Warnings => _warnings;
        public bool HasErrors => _errors.Count > 0;

        /// <summary>임포트를 막는 문제를 기록합니다.</summary>
        public void AddError(string message) => _errors.Add(message);

        /// <summary>임포트는 계속하되 확인이 필요한 문제를 기록합니다.</summary>
        public void AddWarning(string message) => _warnings.Add(message);

        /// <summary>오류와 경고를 한 덩어리 문자열로 만듭니다. 로그와 대화 상자에 씁니다.</summary>
        public string Format()
        {
            StringBuilder builder = new StringBuilder();
            foreach (string error in _errors)
                builder.Append("[오류] ").AppendLine(error);
            foreach (string warning in _warnings)
                builder.Append("[경고] ").AppendLine(warning);
            return builder.ToString();
        }
    }
}
