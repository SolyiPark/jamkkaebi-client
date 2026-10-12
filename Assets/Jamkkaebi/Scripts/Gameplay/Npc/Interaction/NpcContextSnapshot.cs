using System;
using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>한 도깨비의 특성과 상호작용 상태를 복사한 불변 데이터입니다. 현재 상태 모델 구현과는 별개입니다.</summary>
    public sealed class NpcContextSnapshot
    {
        /// <summary>도깨비 인스턴스 식별자입니다. 종류 식별자와 구분하며 대소문자를 보존합니다.</summary>
        public string NpcId { get; }
        /// <summary>게임 콘텐츠가 정의한 도깨비 종류 또는 프로필 식별자입니다.</summary>
        public string DefinitionId { get; }
        /// <summary>화면에 표시할 이름입니다. 한 줄이며 최대 64 UTF-16 코드 단위입니다.</summary>
        public string DisplayName { get; }
        /// <summary>스냅샷의 상호작용 의미 상태 버전입니다. 관련 상태가 바뀔 때 소유자가 갱신합니다.</summary>
        public long StateVersion { get; }
        /// <summary>이 도깨비의 특성을 방어 복사한 읽기 전용 목록입니다.</summary>
        public IReadOnlyList<string> Traits { get; }
        /// <summary>성공한 관리의 누적 횟수입니다. null은 미연결 또는 미확인이며 0과 구분합니다.</summary>
        public int? CareCount { get; }
        /// <summary>0은 깨끗함, 1은 최대 오염인 정규화 값입니다. null은 미확인입니다.</summary>
        public float? Pollution { get; }
        /// <summary>게임이 작성한 최근 사실을 오래된 순서로 복사한 읽기 전용 목록입니다.</summary>
        public IReadOnlyList<string> RecentContext { get; }

        /// <summary>필드 범위와 텍스트를 검사하고 외부 목록을 복사하여 불변 개체 상태를 만듭니다.</summary>
        /// <param name="npcId">도깨비 인스턴스 ID입니다.</param>
        /// <param name="definitionId">도깨비 종류 또는 프로필 ID입니다.</param>
        /// <param name="displayName">제어 문자가 없는 한 줄 표시 이름입니다.</param>
        /// <param name="stateVersion">비교할 상호작용 의미 상태 버전입니다.</param>
        /// <param name="traits">특성 목록입니다. null은 빈 목록으로 복사합니다.</param>
        /// <param name="careCount">0 이상인 성공한 관리 횟수이며 미확인은 null입니다.</param>
        /// <param name="pollution">유한한 0부터 1까지의 오염도이며 미확인은 null입니다.</param>
        /// <param name="recentContext">오래된 순서의 최근 사실입니다. null은 빈 목록입니다.</param>
        /// <exception cref="ArgumentException">ID, 텍스트 또는 목록이 규약을 위반한 경우입니다.</exception>
        /// <exception cref="ArgumentOutOfRangeException">상태 버전이나 관리 횟수가 음수이거나 오염도가 유한한 0부터 1 범위가 아닌 경우입니다.</exception>
        public NpcContextSnapshot(string npcId, string definitionId, string displayName, long stateVersion,
            IEnumerable<string> traits = null, int? careCount = null, float? pollution = null,
            IEnumerable<string> recentContext = null)
        {
            NpcId = NpcInteractionContract.RequireId(npcId, nameof(npcId));
            DefinitionId = NpcInteractionContract.RequireId(definitionId, nameof(definitionId));
            DisplayName = NpcInteractionContract.RequireText(displayName, nameof(displayName),
                NpcInteractionContract.MaxDisplayNameLength);
            if (stateVersion < 0) throw new ArgumentOutOfRangeException(nameof(stateVersion));
            if (careCount.HasValue && careCount.Value < 0) throw new ArgumentOutOfRangeException(nameof(careCount));
            if (pollution.HasValue && (float.IsNaN(pollution.Value) || float.IsInfinity(pollution.Value) ||
                pollution.Value < 0 || pollution.Value > 1))
                throw new ArgumentOutOfRangeException(nameof(pollution));
            StateVersion = stateVersion;
            CareCount = careCount;
            Pollution = pollution;
            Traits = NpcInteractionContract.CopyText(traits, nameof(traits),
                NpcInteractionContract.MaxTraits, NpcInteractionContract.MaxTraitLength);
            RecentContext = NpcInteractionContract.CopyText(recentContext, nameof(recentContext),
                NpcInteractionContract.MaxRecentContextEntries, NpcInteractionContract.MaxContextEntryLength);
        }
    }
}
