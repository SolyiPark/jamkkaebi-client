using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Relic;

namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>
    ///     유물 상태로 다음 판을 정한다. 서버 PlayPlanner에 해당한다.
    ///     최초 정화는 순서와 난이도가 고정이고, 관리 플레이는 선명도가 순서와 고를 수 있는 난이도를 정한다.
    /// </summary>
    public sealed class PlayPlanner
    {
        private readonly ClarityPolicy _clarityPolicy;
        private readonly MinigameDifficulty _firstRouteDifficulty;
        private readonly RestorePolicy _restorePolicy;

        public PlayPlanner(ClarityPolicy clarityPolicy, RestorePolicy restorePolicy,
            MinigameDifficulty firstRouteDifficulty)
        {
            if (clarityPolicy == null) throw new ArgumentNullException(nameof(clarityPolicy));
            if (restorePolicy == null) throw new ArgumentNullException(nameof(restorePolicy));

            _clarityPolicy = clarityPolicy;
            _restorePolicy = restorePolicy;
            _firstRouteDifficulty = firstRouteDifficulty;
        }

        public PlayPlan PlanFor(OwnedRelic relic, DateTime now)
        {
            if (relic == null) throw new ArgumentNullException(nameof(relic));

            if (!relic.IsCompleted)
                return new PlayPlan(EntryRoute.First, PhaseExtensions.From(relic.CurrentPhase),
                    new List<MinigameDifficulty> { _firstRouteDifficulty });

            var clarity = _clarityPolicy.ClarityOf(relic.LastPurifiedAt.Value, now);

            // 서버에서는 남은 페이즈가 비었을 경우 오염 관리 한 판으로 바꾸지만, RestorePhase는 Guard가 될 수 없어 Guard가 항상 남는다.
            // 따라서 빈 remaining에 대한 처리는 하지 않는다.
            var remaining = _clarityPolicy.RemainingPhases(clarity, relic.RestorePhase);

            var difficulties = _restorePolicy.AllowedFor(clarity);

            return new PlayPlan(EntryRoute.Care, remaining, difficulties);
        }
    }
}