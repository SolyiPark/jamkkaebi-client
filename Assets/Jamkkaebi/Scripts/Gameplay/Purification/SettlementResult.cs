using System;
using Jamkkaebi.Scripts.Gameplay.Data;

namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>한 판을 정산한 결과. 결과 팝업이 이 값으로 다음 버튼을 정한다.</summary>
    public sealed class SettlementResult
    {
        public PlayResult Result { get; }
        public NextAction Next { get; }

        /// <summary>다음에 할 판. Done이면 null.</summary>
        public PlayPlan NextPlan { get; }

        /// <summary>이번 판으로 최초 정화를 마쳤는지. 전시관 배치·도감 등록 연출에 쓴다.</summary>
        public bool CompletedNow { get; }

        /// <summary>이번 판으로 오른 공개 단계. 최초 정화 성공일 때만 값이 있다.</summary>
        public RevealLevel? Revealed { get; }

        public SettlementResult(PlayResult result, NextAction next, PlayPlan nextPlan,
            bool completedNow, RevealLevel? revealed)
        {
            if ((next == NextAction.Done) ^ (nextPlan == null))
                throw new ArgumentException("최초 정화 또는 관리가 모두 완료되었다면 다음 계획이 존재하지 않아야 합니다.", nameof(nextPlan));

            if (result == PlayResult.Failure && next != NextAction.Retry)
                throw new ArgumentException("실패한 세션에서는 다음 액션이 재시도여야 합니다.", nameof(next));
            if (completedNow && next != NextAction.Done)
                throw new ArgumentException("최초 정화가 또는 관리가 모두 완료되었다면 다음 액션이 완료여야 합니다.", nameof(next));

            Result = result;
            Next = next;
            NextPlan = nextPlan;
            CompletedNow = completedNow;
            Revealed = revealed;
        }
    }
}