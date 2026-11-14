using System;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Relic;

namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>
    /// 한 판의 결과로 유물 상태를 바꾸고 다음 행동을 정한다. 서버 PurificationService.settle, nextStep에 해당한다.
    /// </summary>
    /// <remarks>넘겨받은 OwnedRelic을 직접 바꾼다. 저장은 호출하는 쪽(백엔드)이 맡는다.</remarks>
    public sealed class PurificationSettlement
    {
        private readonly PlayPlanner _planner;
        private readonly RestorePolicy _restorePolicy;

        public PurificationSettlement(PlayPlanner planner, RestorePolicy restorePolicy)
        {
            if (planner == null) throw new ArgumentNullException(nameof(planner));
            if (restorePolicy == null) throw new ArgumentNullException(nameof(restorePolicy));
            
            _planner = planner;
            _restorePolicy = restorePolicy;
        }

        /// <param name="plan">이 판을 시작할 때 받은 계획.</param>
        /// <param name="difficulty">이 판에서 실제로 고른 난이도.</param>
        /// <exception cref="ArgumentException">계획이 허용하지 않는 난이도인 경우.</exception>
        /// <exception cref="InvalidOperationException">계획이 유물의 지금 상태와 맞지 않는 경우(이미 정산한 결과 등).</exception>
        public SettlementResult Settle(OwnedRelic relic, PlayPlan plan, MinigameDifficulty difficulty,
            PlayResult result, DateTime now)
        {
            if (relic == null) throw new ArgumentNullException(nameof(relic));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!plan.Allows(difficulty))
                throw new ArgumentException("계획이 허용하지 않는 난이도입니다.", nameof(difficulty));
            if (plan.Route == EntryRoute.First)
            {
                if (relic.IsCompleted) throw new InvalidOperationException("완료된 유물에는 최초 정화 경로의 계획을 적용할 수 없습니다.");
                if (plan.Phase != relic.CurrentPhase) throw new InvalidOperationException("이미 정산한 결과입니다.");
            }
            else
            {
                if (!relic.IsCompleted)
                    throw new InvalidOperationException("최초 정화가 완료되지 않은 유물에는 관리 경로의 계획을 적용할 수 없습니다.");
            }

            if (result == PlayResult.Failure)
            {
                return new SettlementResult(result, NextAction.Retry, _planner.PlanFor(relic, now), false, null);
            }

            return plan.Route == EntryRoute.First
                ? SettleFirst(relic, now)
                : SettleCare(relic, plan, difficulty, now);
        }

        private SettlementResult SettleFirst(OwnedRelic relic, DateTime now)
        {
            bool completedNow = relic.ClearFirstPlay(now);

            RevealLevel revealed = relic.RevealLevel;

            if (completedNow)
            {
                return new SettlementResult(PlayResult.Success, NextAction.Done, null, true, revealed);
            }
            return new SettlementResult(PlayResult.Success, NextAction.NextPhase, _planner.PlanFor(relic, now), false, revealed);
        }

        private SettlementResult SettleCare(OwnedRelic relic, PlayPlan plan, MinigameDifficulty difficulty,
            DateTime now)
        {
            RestoreEffect effect = _restorePolicy.EffectOf(difficulty);

            if (effect == RestoreEffect.Full)
            {
                relic.RestoreFully(now);
            }
            else
            {
                relic.ClearCarePlay(plan.Phase, plan.Clarity.Value, now);
            }
            
            bool restored = effect == RestoreEffect.Full || plan.Phase.IsLast();
            
            return restored
                ? new SettlementResult(PlayResult.Success, NextAction.Done, null, false, null)
                : new SettlementResult(PlayResult.Success, NextAction.ContinueRestore, _planner.PlanFor(relic, now), false, null);
        }
    }
}