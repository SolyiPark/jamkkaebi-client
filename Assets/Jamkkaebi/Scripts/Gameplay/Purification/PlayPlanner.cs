using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Relic;

namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>
    /// 유물 상태로 다음 판을 정한다. 서버 PlayPlanner에 해당한다.
    /// 최초 정화는 순서와 난이도가 고정이고, 관리 플레이는 선명도가 순서와 고를 수 있는 난이도를 정한다.
    /// </summary>
    public sealed class PlayPlanner
    {
        private readonly ClarityPolicy _clarityPolicy;
        private readonly MinigameDifficulty _firstRouteDifficulty;
        private readonly RestorePolicy _restorePolicy;

        /// <summary>선명도 계산 정책. 정산도 같은 정책으로 정산 시각의 선명도를 구한다.</summary>
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

            Clarity clarity = _clarityPolicy.ClarityOf(relic.LastPurifiedAt.Value, now);

            // 서버는 relic.restorePhase를 그대로 넘기지만,
            // 여기서는 복구를 시작한 선명도와 지금 선명도가 같을 때만 진행을 인정한다(복구 도중 재봉인되면 봉인 해제부터 다시).
            // RestorePhaseFor는 Guard를 돌려주지 않으므로 Guard가 항상 남아, 서버처럼 빈 목록을 처리할 필요가 없다.
            IReadOnlyList<Phase> remaining = _clarityPolicy.RemainingPhases(clarity, relic.RestorePhaseFor(clarity));

            IReadOnlyList<MinigameDifficulty> difficulties = _restorePolicy.AllowedFor(clarity);

            return new PlayPlan(EntryRoute.Care, remaining, difficulties, clarity);
        }
    }
}