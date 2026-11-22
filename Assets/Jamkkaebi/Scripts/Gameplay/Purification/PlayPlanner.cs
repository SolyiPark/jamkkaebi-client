using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Relic;

namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>
    /// 유물 상태로 다음 판을 정한다. 서버 PlayPlanner에 해당한다.
    /// 최초 정화는 순서와 난이도가 고정이고, 관리 플레이는 선명도가 이번 페이즈와 고를 수 있는 난이도를 정한다.
    /// </summary>
    public sealed class PlayPlanner
    {
        private readonly ClarityPolicy _clarityPolicy;
        private readonly MinigameDifficulty _firstRouteDifficulty;
        private readonly RestorePolicy _restorePolicy;

        /// <summary>선명도 계산 정책. 정산도 같은 정책으로 오른 선명도의 기준 시각을 정한다.</summary>
        public ClarityPolicy ClarityPolicy => _clarityPolicy;

        /// <summary>계획에서 고를 수 있는 난이도를 정한 복구 정책. 정산도 같은 정책을 써야 한다.</summary>
        public RestorePolicy RestorePolicy => _restorePolicy;

        public PlayPlanner(ClarityPolicy clarityPolicy, RestorePolicy restorePolicy,
            MinigameDifficulty firstRouteDifficulty)
        {
            if (clarityPolicy == null) throw new ArgumentNullException(nameof(clarityPolicy));
            if (restorePolicy == null) throw new ArgumentNullException(nameof(restorePolicy));
            if (!Enum.IsDefined(typeof(MinigameDifficulty), firstRouteDifficulty))
                throw new ArgumentException("존재하지 않는 최초 정화 난이도입니다.", nameof(firstRouteDifficulty));

            _clarityPolicy = clarityPolicy;
            _restorePolicy = restorePolicy;
            _firstRouteDifficulty = firstRouteDifficulty;
        }

        public PlayPlan PlanFor(OwnedRelic relic, DateTime now)
        {
            if (relic == null) throw new ArgumentNullException(nameof(relic));

            if (!relic.IsCompleted)
                return new PlayPlan(EntryRoute.First, PhaseExtensions.From(relic.CurrentPhase),
                    new List<MinigameDifficulty> { _firstRouteDifficulty }, null);

            // 선명도가 곧 복구 진행이다. 따로 저장한 복구 진행 없이 지금 선명도로 이번 판과 남은 순서를 정한다.
            Clarity clarity = _clarityPolicy.ClarityOf(relic.ClarityAnchorAt.Value, now);
            IReadOnlyList<Phase> remaining = _clarityPolicy.RemainingPhases(clarity);

            IReadOnlyList<MinigameDifficulty> difficulties = _restorePolicy.AllowedFor(clarity);

            return new PlayPlan(EntryRoute.Care, remaining, difficulties, clarity);
        }
    }
}