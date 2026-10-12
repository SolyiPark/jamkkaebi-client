using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>NPC 상호작용 v1의 형식과 크기 제한입니다. 추론 엔진이나 게임 밸런스에 의존하지 않습니다.</summary>
    public static class NpcInteractionContract
    {
        /// <summary>공통 규약 버전입니다. 데이터 계약 변경 시 호환성을 검토합니다.</summary>
        public const int Version = 1;
        /// <summary>식별자의 최대 길이입니다. UTF-16 코드 단위로 측정합니다.</summary>
        public const int MaxIdLength = 64;
        /// <summary>이름과 선택지 라벨의 최대 UTF-16 길이입니다.</summary>
        public const int MaxDisplayNameLength = 64;
        /// <summary>스냅샷에 포함할 수 있는 특성 항목의 최대 개수입니다.</summary>
        public const int MaxTraits = 8;
        /// <summary>특성 한 항목의 최대 UTF-16 길이입니다.</summary>
        public const int MaxTraitLength = 80;
        /// <summary>최근 사실 요약의 최대 항목 수입니다.</summary>
        public const int MaxRecentContextEntries = 8;
        /// <summary>최근 기록, 의도, 사실, 불가 사유의 최대 UTF-16 길이입니다.</summary>
        public const int MaxContextEntryLength = 160;
        /// <summary>출력 계획이 전달할 확정 사실의 최대 항목 수입니다.</summary>
        public const int MaxFacts = 8;
        /// <summary>현재 또는 다음 노드의 최대 선택지 수입니다. 실제 메뉴 개수는 콘텐츠가 정합니다.</summary>
        public const int MaxChoices = 8;
        /// <summary>한 줄 대사의 최대 UTF-16 길이입니다. 토큰 수나 화면 글자 수가 아닙니다.</summary>
        public const int MaxDialogueLength = 160;

        /// <summary>비어 있거나 너무 길거나 제어 문자가 있는 문자열을 거부합니다.</summary>
        /// <param name="value">검사할 원본 문자열입니다. 자동 정규화하지 않습니다.</param>
        /// <param name="parameter">예외에 기록할 호출 측 매개변수 이름입니다.</param>
        /// <param name="maxLength">허용하는 최대 UTF-16 길이입니다.</param>
        /// <returns>변경하지 않은 입력 문자열입니다.</returns>
        /// <exception cref="ArgumentException">문자열이 비어 있거나 길이를 초과하거나 제어 문자를 포함한 경우입니다.</exception>
        internal static string RequireText(string value, string parameter, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
                throw new ArgumentException("비어 있지 않은 제한 길이 이내의 텍스트가 필요합니다.", parameter);
            foreach (char character in value)
            {
                if (char.IsControl(character))
                    throw new ArgumentException("텍스트에 줄바꿈 또는 제어 문자를 넣을 수 없습니다.", parameter);
            }
            return value;
        }

        /// <summary>식별자의 길이와 영문, 숫자, 하이픈, 밑줄, 점, 콜론만 허용하는 형식을 검사합니다.</summary>
        /// <param name="value">검사할 원본 문자열입니다. 자동 정규화하지 않습니다.</param>
        /// <param name="parameter">예외에 기록할 호출 측 매개변수 이름입니다.</param>
        /// <returns>대소문자와 공백을 자동 보정하지 않은 유효 ID입니다.</returns>
        /// <exception cref="ArgumentException">ID가 비어 있거나 길이 또는 허용 문자 규칙을 위반한 경우입니다.</exception>
        internal static string RequireId(string value, string parameter)
        {
            RequireText(value, parameter, MaxIdLength);
            foreach (char character in value)
            {
                if (!((character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9') ||
                    character == '-' || character == '_' || character == '.' || character == ':'))
                    throw new ArgumentException("ID에는 영문·숫자·하이픈·밑줄·점·콜론만 사용할 수 있습니다.", parameter);
            }
            return value;
        }

        /// <summary>항목 수와 텍스트를 검사하고 호출 측 변경의 영향을 받지 않는 읽기 전용 복사본을 만듭니다.</summary>
        /// <param name="values">복사할 문자열 목록입니다. null은 빈 목록입니다.</param>
        /// <param name="parameter">예외에 기록할 호출 측 매개변수 이름입니다.</param>
        /// <param name="maxCount">허용하는 최대 항목 수입니다.</param>
        /// <param name="maxLength">허용하는 최대 UTF-16 길이입니다.</param>
        /// <returns>원본에서 분리한 읽기 전용 문자열 목록입니다.</returns>
        /// <exception cref="ArgumentException">항목 수 또는 항목 텍스트가 규약을 위반한 경우입니다.</exception>
        internal static ReadOnlyCollection<string> CopyText(IEnumerable<string> values,
            string parameter, int maxCount, int maxLength)
        {
            var copy = new List<string>();
            if (values != null)
            {
                foreach (string value in values)
                {
                    if (copy.Count >= maxCount)
                        throw new ArgumentException("목록의 최대 항목 수를 초과했습니다.", parameter);
                    copy.Add(RequireText(value, parameter, maxLength));
                }
            }
            return copy.AsReadOnly();
        }

        /// <summary>null 항목과 중복 행동 ID 및 항목 수 초과를 거부하고 선택지를 복사합니다.</summary>
        /// <param name="choices">현재 선택지입니다. null은 빈 목록이며 중복 행동 ID는 허용하지 않습니다.</param>
        /// <param name="parameter">예외에 기록할 호출 측 매개변수 이름입니다.</param>
        /// <returns>검사 후 복사한 읽기 전용 선택지 목록입니다.</returns>
        /// <exception cref="ArgumentException">null 항목, 중복 행동 ID 또는 항목 수 초과가 있는 경우입니다.</exception>
        internal static ReadOnlyCollection<NpcInteractionChoice> CopyChoices(
            IEnumerable<NpcInteractionChoice> choices, string parameter)
        {
            var copy = new List<NpcInteractionChoice>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (choices != null)
            {
                foreach (NpcInteractionChoice choice in choices)
                {
                    if (choice == null || copy.Count >= MaxChoices || !ids.Add(choice.ActionId))
                        throw new ArgumentException("선택지는 null·중복 ID 없이 최대 항목 수 이내여야 합니다.", parameter);
                    copy.Add(choice);
                }
            }
            return copy.AsReadOnly();
        }
    }
}
