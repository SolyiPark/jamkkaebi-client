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

        /// <remarks>복구 정책은 따로 받지 않고 계획기의 것을 쓴다. 계획이 허용한 난이도와 정산이 적용하는 효과가 어긋나지 않게 한다.</remarks>
        public PurificationSettlement(PlayPlanner planner)
        {
            if (planner == null) throw new ArgumentNullException(nameof(planner));

            _planner = planner;
            _restorePolicy = planner.RestorePolicy;
        }

        /// <remarks>
        /// 한 판의 결과는 한 번만 정산한다. 같은 결과의 중복·지난 보고는 여기서가 아니라 세션 계층에서 막는다
        /// (서버처럼 판을 시작할 때 1회용 세션 토큰을 발급하고, 보고는 그 토큰으로 한 번만 받는다).
        /// 관리 경로는 시작 시점 계획을 그대로 인정하므로(7·14일 경계 통과 허용) 지금 상태와 비교해 거르지 않는다.
        /// </remarks>
        /// <param name="plan">이 판을 시작할 때 받은 계획.</param>
        /// <param name="difficulty">이 판에서 실제로 고른 난이도.</param>
        /// <exception cref="ArgumentException">계획이 허용하지 않는 난이도인 경우, 정의되지 않은 결과인 경우.</exception>
        /// <exception cref="InvalidOperationException">계획이 유물의 지금 상태와 맞지 않는 경우(이미 정산한 결과 등).</exception>
        public SettlementResult Settle(OwnedRelic relic, PlayPlan plan, MinigameDifficulty difficulty,
            PlayResult result, DateTime now)
        {
            if (relic == null) throw new ArgumentNullException(nameof(relic));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!plan.Allows(difficulty))
                throw new ArgumentException("계획이 허용하지 않는 난이도입니다.", nameof(difficulty));
            if (!Enum.IsDefined(typeof(PlayResult), result)) throw new ArgumentException($"정화 결과 {result}는 정의되지 않았습니다.", nameof(result));
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