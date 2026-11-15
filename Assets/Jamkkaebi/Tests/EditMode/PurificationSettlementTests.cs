using System;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class PurificationSettlementTests
    {
        private const int RelicId = 2;
        private static readonly DateTime AcquiredAt = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime LastPurifiedAt = AcquiredAt + TimeSpan.FromHours(1);

        private PlayPlanner _planner;
        private PurificationSettlement _settlement;

        [SetUp]
        public void SetUp()
        {
            // 흐려짐 7일, 재봉인 14일, 최초 정화는 보통
            _planner = new PlayPlanner(
                new ClarityPolicy(TimeSpan.FromDays(7), TimeSpan.FromDays(14)),
                RestorePolicy.Default,
                MinigameDifficulty.Normal);
            _settlement = new PurificationSettlement(_planner);
        }

        /// <summary>최초 정화를 마친 유물(마지막 정화 시각 LastPurifiedAt).</summary>
        private static OwnedRelic CreateCompleted()
        {
            return new OwnedRelic(RelicId, AcquiredAt, RelicStatus.Completed, Phase.Guard,
                LastPurifiedAt, null, null, 0);
        }

        private static DateTime DaysAfterPurified(double days) => LastPurifiedAt + TimeSpan.FromDays(days);

        // ==============================
        //      최초 정화
        // ==============================

        /// <summary>중간 페이즈 성공이면 다음 페이즈로 넘어가고 공개 단계가 오르는지 검사합니다.</summary>
        [Test]
        public void Settle_FirstSuccess_NextPhase()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);
            PlayPlan plan = _planner.PlanFor(relic, AcquiredAt);

            SettlementResult result = _settlement.Settle(relic, plan, MinigameDifficulty.Normal,
                PlayResult.Success, AcquiredAt);

            Assert.AreEqual(PlayResult.Success, result.Result);
            Assert.AreEqual(NextAction.NextPhase, result.Next);
            Assert.AreEqual(Phase.Recall, result.NextPlan.Phase);
            Assert.AreEqual(EntryRoute.First, result.NextPlan.Route);
            Assert.AreEqual(RevealLevel.Form, result.Revealed);
            Assert.IsFalse(result.CompletedNow);
            Assert.AreEqual(Phase.Recall, relic.CurrentPhase);
        }

        /// <summary>마지막 페이즈 성공이면 완료되고 Done인지 검사합니다.</summary>
        [Test]
        public void Settle_FirstLastPhase_CompletesAndDone()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);
            for (int i = 0; i < 2; i++)
            {
                _settlement.Settle(relic, _planner.PlanFor(relic, AcquiredAt), MinigameDifficulty.Normal,
                    PlayResult.Success, AcquiredAt);
            }

            SettlementResult result = _settlement.Settle(relic, _planner.PlanFor(relic, AcquiredAt),
                MinigameDifficulty.Normal, PlayResult.Success, LastPurifiedAt);

            Assert.AreEqual(NextAction.Done, result.Next);
            Assert.IsNull(result.NextPlan);
            Assert.IsTrue(result.CompletedNow);
            Assert.AreEqual(RevealLevel.Named, result.Revealed);
            Assert.IsTrue(relic.IsCompleted);
            Assert.AreEqual(LastPurifiedAt, relic.LastPurifiedAt);
        }

        /// <summary>실패면 상태를 바꾸지 않고 같은 페이즈를 다시 하는지 검사합니다.</summary>
        [Test]
        public void Settle_FirstFailure_RetryWithoutChange()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);
            PlayPlan plan = _planner.PlanFor(relic, AcquiredAt);

            SettlementResult result = _settlement.Settle(relic, plan, MinigameDifficulty.Normal,
                PlayResult.Failure, AcquiredAt);

            Assert.AreEqual(NextAction.Retry, result.Next);
            Assert.AreEqual(Phase.Unseal, result.NextPlan.Phase);
            Assert.IsNull(result.Revealed);
            Assert.AreEqual(Phase.Unseal, relic.CurrentPhase);
        }

        /// <summary>같은 결과가 두 번 정산되면 두 번째를 거부하고 한 번만 진행하는지 검사합니다.</summary>
        [Test]
        public void Settle_FirstTwiceWithSamePlan_SecondThrows()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);
            PlayPlan plan = _planner.PlanFor(relic, AcquiredAt);
            _settlement.Settle(relic, plan, MinigameDifficulty.Normal, PlayResult.Success, AcquiredAt);

            Assert.Throws<InvalidOperationException>(() => _settlement.Settle(relic, plan,
                MinigameDifficulty.Normal, PlayResult.Success, AcquiredAt));
            Assert.AreEqual(Phase.Recall, relic.CurrentPhase);
        }

        /// <summary>최초 정화 계획을 이미 완료한 유물에 정산하면 거부하는지 검사합니다.</summary>
        [Test]
        public void Settle_FirstPlanOnCompletedRelic_Throws()
        {
            PlayPlan firstPlan = _planner.PlanFor(OwnedRelic.Acquired(RelicId, AcquiredAt), AcquiredAt);

            Assert.Throws<InvalidOperationException>(() => _settlement.Settle(CreateCompleted(), firstPlan,
                MinigameDifficulty.Normal, PlayResult.Success, AcquiredAt));
        }

        /// <summary>계획이 허용하지 않는 난이도면 거부하고 상태를 바꾸지 않는지 검사합니다.</summary>
        [Test]
        public void Settle_DisallowedDifficulty_ThrowsWithoutChange()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);
            PlayPlan plan = _planner.PlanFor(relic, AcquiredAt);

            Assert.Throws<ArgumentException>(() => _settlement.Settle(relic, plan, MinigameDifficulty.Hard,
                PlayResult.Success, AcquiredAt));
            Assert.AreEqual(Phase.Unseal, relic.CurrentPhase);
        }

        /// <summary>정의되지 않은 결과면 최초 정화를 진행하지 않고 거부하는지 검사합니다.</summary>
        [Test]
        public void Settle_FirstUndefinedResult_ThrowsWithoutChange()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);
            PlayPlan plan = _planner.PlanFor(relic, AcquiredAt);

            Assert.Throws<ArgumentException>(() => _settlement.Settle(relic, plan, MinigameDifficulty.Normal,
                (PlayResult)99, AcquiredAt));
            Assert.AreEqual(Phase.Unseal, relic.CurrentPhase);
            Assert.AreEqual(RelicStatus.InProgress, relic.Status);
        }

        /// <summary>정의되지 않은 결과면 관리 플레이의 돌봄·선명도 회복을 반영하지 않고 거부하는지 검사합니다.</summary>
        [Test]
        public void Settle_CareUndefinedResult_ThrowsWithoutChange()
        {
            OwnedRelic relic = CreateCompleted();
            PlayPlan plan = _planner.PlanFor(relic, DaysAfterPurified(3));

            Assert.Throws<ArgumentException>(() => _settlement.Settle(relic, plan, MinigameDifficulty.Hard,
                (PlayResult)99, DaysAfterPurified(3)));
            Assert.AreEqual(0, relic.CareCount);
            Assert.AreEqual(LastPurifiedAt, relic.LastPurifiedAt);
        }

        /// <summary>정의되지 않은 결과는 이미 정산한 계획인지 보기 전에 입력 오류로 거부하는지 검사합니다.</summary>
        [Test]
        public void Settle_UndefinedResultWithStalePlan_ThrowsArgumentFirst()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);
            PlayPlan plan = _planner.PlanFor(relic, AcquiredAt);
            _settlement.Settle(relic, plan, MinigameDifficulty.Normal, PlayResult.Success, AcquiredAt);

            Assert.Throws<ArgumentException>(() => _settlement.Settle(relic, plan, MinigameDifficulty.Normal,
                (PlayResult)99, AcquiredAt));
        }

        // ==============================
        //      관리 플레이
        // ==============================

        /// <summary>선명한 정령을 쉬움으로 돌보면 회복되고 Done이며 돌봄이 1 오르는지 검사합니다.</summary>
        [Test]
        public void Settle_CareClearEasy_RefreshesAndDone()
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = DaysAfterPurified(3);
            PlayPlan plan = _planner.PlanFor(relic, now);

            SettlementResult result = _settlement.Settle(relic, plan, MinigameDifficulty.Easy,
                PlayResult.Success, now);

            Assert.AreEqual(NextAction.Done, result.Next);
            Assert.IsNull(result.NextPlan);
            Assert.IsFalse(result.CompletedNow);
            Assert.IsNull(result.Revealed);
            Assert.AreEqual(now, relic.LastPurifiedAt);
            Assert.AreEqual(1, relic.CareCount);
        }

        /// <summary>재봉인 정령을 보통으로 봉인 해제하면 한 단계만 복구되고 기억 회복이 이어지는지 검사합니다.</summary>
        [Test]
        public void Settle_CareResealedNormal_ContinuesRestore()
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = DaysAfterPurified(15);
            PlayPlan plan = _planner.PlanFor(relic, now);

            SettlementResult result = _settlement.Settle(relic, plan, MinigameDifficulty.Normal,
                PlayResult.Success, now);

            Assert.AreEqual(NextAction.ContinueRestore, result.Next);
            Assert.AreEqual(EntryRoute.Care, result.NextPlan.Route);
            Assert.AreEqual(Phase.Recall, result.NextPlan.Phase);
            Assert.AreEqual(Phase.Unseal, relic.RestorePhase);
            Assert.AreEqual(Clarity.Resealed, relic.RestoreClarity);
            Assert.AreEqual(LastPurifiedAt, relic.LastPurifiedAt);
            Assert.AreEqual(1, relic.CareCount);
        }

        /// <summary>재봉인 정령을 보통으로 세 판 복구하면 마지막에 Done이고 돌봄이 3 오르는지 검사합니다.</summary>
        [Test]
        public void Settle_CareResealedNormal_ThreePlaysToDone()
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = DaysAfterPurified(15);
            SettlementResult result = null;

            for (int i = 0; i < 3; i++)
            {
                result = _settlement.Settle(relic, _planner.PlanFor(relic, now), MinigameDifficulty.Normal,
                    PlayResult.Success, now);
            }

            Assert.AreEqual(NextAction.Done, result.Next);
            Assert.AreEqual(now, relic.LastPurifiedAt);
            Assert.IsNull(relic.RestorePhase);
            Assert.AreEqual(3, relic.CareCount);
        }

        /// <summary>재봉인 정령을 어려움으로 돌보면 한 판에 전체 복구되는지 검사합니다.</summary>
        [Test]
        public void Settle_CareResealedHard_FullRestoreAndDone()
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = DaysAfterPurified(15);
            PlayPlan plan = _planner.PlanFor(relic, now);

            SettlementResult result = _settlement.Settle(relic, plan, MinigameDifficulty.Hard,
                PlayResult.Success, now);

            Assert.AreEqual(NextAction.Done, result.Next);
            Assert.IsNull(result.NextPlan);
            Assert.AreEqual(now, relic.LastPurifiedAt);
            Assert.IsNull(relic.RestorePhase);
            Assert.AreEqual(1, relic.CareCount);
        }

        /// <summary>흐려지거나 재봉인된 정령은 쉬움으로 정산할 수 없는지 검사합니다.</summary>
        [TestCase(8)]
        [TestCase(15)]
        public void Settle_CareNotClearEasy_Throws(int days)
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = DaysAfterPurified(days);
            PlayPlan plan = _planner.PlanFor(relic, now);

            Assert.Throws<ArgumentException>(() => _settlement.Settle(relic, plan, MinigameDifficulty.Easy,
                PlayResult.Success, now));
            Assert.AreEqual(0, relic.CareCount);
        }

        /// <summary>관리 플레이 실패면 돌봄·복구 진행이 바뀌지 않고 Retry인지 검사합니다.</summary>
        [Test]
        public void Settle_CareFailure_RetryWithoutChange()
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = DaysAfterPurified(15);
            PlayPlan plan = _planner.PlanFor(relic, now);

            SettlementResult result = _settlement.Settle(relic, plan, MinigameDifficulty.Normal,
                PlayResult.Failure, now);

            Assert.AreEqual(NextAction.Retry, result.Next);
            Assert.AreEqual(Phase.Unseal, result.NextPlan.Phase);
            Assert.AreEqual(0, relic.CareCount);
            Assert.IsNull(relic.RestorePhase);
            Assert.AreEqual(LastPurifiedAt, relic.LastPurifiedAt);
        }

        /// <summary>
        /// 흐려짐에서 시작한 판이 재봉인 경계를 넘어 실패하면, 재시도 계획은 지금 상태(봉인 해제부터)로 만들어지는지 검사합니다.
        /// (기획 결정: 다음 계획은 정산 시점 상태로)
        /// </summary>
        [Test]
        public void Settle_CareFailureAcrossBoundary_RetryPlanUsesCurrentState()
        {
            OwnedRelic relic = CreateCompleted();
            PlayPlan fadedPlan = _planner.PlanFor(relic, DaysAfterPurified(13.9));
            Assert.AreEqual(Phase.Recall, fadedPlan.Phase);

            SettlementResult result = _settlement.Settle(relic, fadedPlan, MinigameDifficulty.Normal,
                PlayResult.Failure, DaysAfterPurified(14.1));

            Assert.AreEqual(NextAction.Retry, result.Next);
            Assert.AreEqual(Clarity.Resealed, result.NextPlan.Clarity);
            Assert.AreEqual(Phase.Unseal, result.NextPlan.Phase);
            Assert.AreEqual(0, relic.CareCount);
        }

        /// <summary>관리 플레이 계획을 최초 정화 중인 유물에 정산하면 거부하는지 검사합니다.</summary>
        [Test]
        public void Settle_CarePlanOnInProgressRelic_Throws()
        {
            PlayPlan carePlan = _planner.PlanFor(CreateCompleted(), DaysAfterPurified(15));

            Assert.Throws<InvalidOperationException>(() => _settlement.Settle(
                OwnedRelic.Acquired(RelicId, AcquiredAt), carePlan, MinigameDifficulty.Normal,
                PlayResult.Success, AcquiredAt));
        }

        // ==============================
        //      플레이 도중 선명도 경계를 넘음 (2026-11-15 결정: 유예 없음)
        // ==============================

        /// <summary>선명할 때 시작한 오염 관리가 흐려짐 경계를 넘어 끝나도 시작 시점 기준으로 회복되는지 검사합니다.</summary>
        [Test]
        public void Settle_GuardCrossesFadedBoundary_StillRestores()
        {
            OwnedRelic relic = CreateCompleted();
            PlayPlan plan = _planner.PlanFor(relic, DaysAfterPurified(7) - TimeSpan.FromMinutes(1));
            DateTime now = DaysAfterPurified(7) + TimeSpan.FromMinutes(1);

            SettlementResult result = _settlement.Settle(relic, plan, MinigameDifficulty.Easy,
                PlayResult.Success, now);

            Assert.AreEqual(NextAction.Done, result.Next);
            Assert.AreEqual(now, relic.LastPurifiedAt);
        }

        /// <summary>
        /// 흐려짐에서 시작한 기억 회복이 재봉인 경계를 넘어 끝나면, 진행이 정산 순간의 선명도(재봉인)로 저장되어
        /// 다음 계획은 오염 관리만 남는지 검사합니다. (서버와 같은 관대한 규칙, 2026-11-16)
        /// </summary>
        [Test]
        public void Settle_MiddlePhaseCrossesResealedBoundary_KeepsProgressAtSettledClarity()
        {
            OwnedRelic relic = CreateCompleted();
            PlayPlan plan = _planner.PlanFor(relic, DaysAfterPurified(14) - TimeSpan.FromMinutes(1));
            DateTime now = DaysAfterPurified(14) + TimeSpan.FromMinutes(1);
            Assert.AreEqual(Phase.Recall, plan.Phase);

            SettlementResult result = _settlement.Settle(relic, plan, MinigameDifficulty.Normal,
                PlayResult.Success, now);

            Assert.AreEqual(1, relic.CareCount);
            Assert.AreEqual(Phase.Recall, relic.RestorePhase);
            Assert.AreEqual(Clarity.Resealed, relic.RestoreClarity);
            Assert.AreEqual(NextAction.ContinueRestore, result.Next);
            CollectionAssert.AreEqual(new[] { Phase.Guard }, result.NextPlan.RemainingPhases);
        }

        /// <summary>
        /// 판과 판 사이에 재봉인 경계를 넘으면(이번 판은 흐려짐 안에서 끝남) 지난 진행을 버리고 봉인 해제부터인지 검사합니다.
        /// (7번 규칙은 그대로)
        /// </summary>
        [Test]
        public void Settle_ResealedBetweenPlays_RestartsFromUnseal()
        {
            OwnedRelic relic = CreateCompleted();
            PlayPlan plan = _planner.PlanFor(relic, DaysAfterPurified(10));
            _settlement.Settle(relic, plan, MinigameDifficulty.Normal, PlayResult.Success, DaysAfterPurified(10));
            Assert.AreEqual(Clarity.Faded, relic.RestoreClarity);

            PlayPlan next = _planner.PlanFor(relic, DaysAfterPurified(15));

            CollectionAssert.AreEqual(new[] { Phase.Unseal, Phase.Recall, Phase.Guard }, next.RemainingPhases);
        }

        // ==============================
        //      인자 검사
        // ==============================

        /// <summary>null 인자를 거부하는지 검사합니다.</summary>
        [Test]
        public void NullArguments_Throw()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);
            PlayPlan plan = _planner.PlanFor(relic, AcquiredAt);

            Assert.Throws<ArgumentNullException>(() => new PurificationSettlement(null));
            Assert.Throws<ArgumentNullException>(() => _settlement.Settle(null, plan,
                MinigameDifficulty.Normal, PlayResult.Success, AcquiredAt));
            Assert.Throws<ArgumentNullException>(() => _settlement.Settle(relic, null,
                MinigameDifficulty.Normal, PlayResult.Success, AcquiredAt));
        }

        /// <summary>
        /// 정산이 계획기와 같은 복구 정책을 쓰는지 검사합니다.
        /// (쉬움을 전체 복구로 정한 정책이면, 흐려진 정령에 쉬움이 허용되고 한 판에 회복된다)
        /// </summary>
        [Test]
        public void Settle_UsesPlannersRestorePolicy()
        {
            RestorePolicy easyFull = new RestorePolicy(new System.Collections.Generic.Dictionary<MinigameDifficulty, RestoreEffect>
            {
                { MinigameDifficulty.Easy, RestoreEffect.Full },
                { MinigameDifficulty.Normal, RestoreEffect.OneStep }
            });
            PlayPlanner planner = new PlayPlanner(
                new ClarityPolicy(TimeSpan.FromDays(7), TimeSpan.FromDays(14)), easyFull, MinigameDifficulty.Normal);
            PurificationSettlement settlement = new PurificationSettlement(planner);
            OwnedRelic relic = CreateCompleted();
            PlayPlan plan = planner.PlanFor(relic, DaysAfterPurified(8));

            Assert.AreSame(easyFull, planner.RestorePolicy);
            Assert.IsTrue(plan.Allows(MinigameDifficulty.Easy));

            SettlementResult result = settlement.Settle(relic, plan, MinigameDifficulty.Easy,
                PlayResult.Success, DaysAfterPurified(8));

            Assert.AreEqual(NextAction.Done, result.Next);
            Assert.AreEqual(DaysAfterPurified(8), relic.LastPurifiedAt);
            Assert.AreEqual(1, relic.CareCount);
        }
    }
}
