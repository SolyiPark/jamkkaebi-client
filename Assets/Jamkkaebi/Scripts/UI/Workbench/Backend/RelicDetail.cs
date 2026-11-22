using System;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>유물 하나의 상세. 서버 RelicDetailResponse 중 정화에 필요한 부분.</summary>
    public sealed class RelicDetail
    {
        public int RelicId { get; }
        public RelicStatus Status { get; }

        /// <summary>정령 이름(사용자 이름, 없으면 기본 이름). 최초 정화를 마친 뒤에만 공개되므로 진행 중이면 null.</summary>
        public string SpiritName { get; }

        /// <summary>최초 정화 중이면 null.</summary>
        public Clarity? Clarity { get; }

        /// <summary>마지막으로 정화에 성공한 시각("마지막 정화로부터 n일째"). 최초 정화 중이면 null.</summary>
        public DateTime? LastPurifiedAt { get; }

        /// <summary>이대로 두면 선명도가 한 칸 떨어지는 시각(선명 → 흐려짐, 흐려짐 → 재봉인). 최초 정화 중이거나 이미 재봉인됐으면 null.</summary>
        public DateTime? ClarityChangesAt { get; }

        public int CareCount { get; }
        public PlayPlan NextPlay { get; }

        public RelicDetail(int relicId, RelicStatus status, string spiritName, Clarity? clarity,
            DateTime? lastPurifiedAt, DateTime? clarityChangesAt, int careCount, PlayPlan nextPlay)
        {
            if (relicId <= 0) throw new ArgumentOutOfRangeException(nameof(relicId), "relicId는 1부터 시작합니다.");
            if (!Enum.IsDefined(typeof(RelicStatus), status))
                throw new ArgumentException($"유물 상태 {status}는 정의되지 않았습니다.", nameof(status));
            if (clarity.HasValue && !Enum.IsDefined(typeof(Clarity), clarity))
                throw new ArgumentException($"선명도 {clarity}는 정의되지 않았습니다.", nameof(clarity));
            if (careCount < 0) throw new ArgumentOutOfRangeException(nameof(careCount), "돌봄 횟수는 0회 이상이어야 합니다.");
            if (nextPlay == null) throw new ArgumentNullException(nameof(nextPlay));
            if (status == RelicStatus.InProgress)
            {
                if (spiritName != null)
                    throw new ArgumentException("최초 정화 진행 중인 유물은 정령 이름이 공개되지 않아야 합니다.", nameof(spiritName));
                if (clarity.HasValue)
                    throw new ArgumentException("최초 정화 진행 중인 유물에는 선명도가 존재하지 않아야 합니다.", nameof(clarity));
                if (clarityChangesAt.HasValue)
                    throw new ArgumentException("최초 정화 진행 중인 유물은 선명도가 바뀌는 시각이 존재하지 않아야 합니다.", nameof(clarityChangesAt));
                if (careCount != 0) throw new ArgumentException("최초 정화 진행 중인 유물은 돌봄 횟수가 0회여야 합니다.", nameof(careCount));
            }
            else
            {
                if (string.IsNullOrEmpty(spiritName))
                    throw new ArgumentException("정화를 완료한 유물에는 정령 이름이 있어야 합니다.", nameof(spiritName));
                if (clarity == null) throw new ArgumentException("정화를 완료한 유물에는 선명도가 존재해야 합니다.", nameof(clarity));
                if (lastPurifiedAt == null)
                    throw new ArgumentException("정화를 완료한 유물에는 마지막 정화 시각이 있어야 합니다.", nameof(lastPurifiedAt));
                if (clarity != Gameplay.Data.Clarity.Resealed && clarityChangesAt == null)
                    throw new ArgumentException("아직 재봉인되지 않은 정령에는 선명도가 바뀌는 시각이 있어야 합니다.", nameof(clarityChangesAt));
                if (clarity == Gameplay.Data.Clarity.Resealed && clarityChangesAt.HasValue)
                    throw new ArgumentException("재봉인된 정령은 더 떨어질 선명도가 없어 바뀌는 시각이 없어야 합니다.", nameof(clarityChangesAt));
            }

            RelicId = relicId;
            Status = status;
            SpiritName = spiritName;
            Clarity = clarity;
            LastPurifiedAt = lastPurifiedAt;
            ClarityChangesAt = clarityChangesAt;
            CareCount = careCount;
            NextPlay = nextPlay;
        }
    }
}