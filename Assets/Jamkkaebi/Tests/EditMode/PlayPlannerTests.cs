using System;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class PlayPlannerTests
    {
        private const int RelicId = 2;
        private static readonly DateTime AcquiredAt = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime LastPurifiedAt = AcquiredAt + TimeSpan.FromHours(1);

        private PlayPlanner _planner;

        [SetUp]
        public void SetUp()
        {
            // 흐려짐 7일, 재봉인 14일
            _planner = new PlayPlanner(
                new ClarityPolicy(TimeSpan.FromDays(7), TimeSpan.FromDays(14)),
                RestorePolicy.Default,
                MinigameDifficulty.Normal);
        }

        /// <summary>최초 정화를 마친 유물. 선명도 기준 시각을 따로 줄 수 있다(없으면 마지막 정화 시각).</summary>
        private static OwnedRelic CreateCompleted(DateTime? clarityAnchorAt = null)
        {
            return new OwnedRelic(RelicId, AcquiredAt, RelicStatus.Completed, Phase.Guard,
                LastPurifiedAt, clarityAnchorAt ?? LastPurifiedAt, 0);
        }

        /// <summary>새로 획득한 유물은 최초 정화로 봉인 해제부터, 난이도 고정으로 시작하는지 검사합니다.</summary>
        [Test]
        public void PlanFor_Acquired_FirstRouteFromUnseal()
        {
            PlayPlan plan = _planner.PlanFor(OwnedRelic.Acquired(RelicId, AcquiredAt), AcquiredAt);

            Assert.AreEqual(EntryRoute.First, plan.Route);
            Assert.IsNull(plan.Clarity);
            Assert.AreEqual(Phase.Unseal, plan.Phase);
            CollectionAssert.AreEqual(new[] { Phase.Unseal, Phase.Recall, Phase.Guard }, plan.RemainingPhases);
            CollectionAssert.AreEqual(new[] { MinigameDifficulty.Normal }, plan.AllowedDifficulties);
            Assert.IsFalse(plan.DifficultySelectable);
        }

        /// <summary>최초 정화 도중이면 다음에 할 페이즈부터 시작하는지 검사합니다.</summary>
        [Test]
        public void PlanFor_InProgress_StartsFromCurrentPhase()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);
            relic.ClearFirstPlay(AcquiredAt);

            PlayPlan plan = _planner.PlanFor(relic, AcquiredAt);

            Assert.AreEqual(EntryRoute.First, plan.Route);
            Assert.AreEqual(Phase.Recall, plan.Phase);
            CollectionAssert.AreEqual(new[] { Phase.Recall, Phase.Guard }, plan.RemainingPhases);
        }

        /// <summary>최초 정화 난이도는 생성자로 받은 값을 쓰는지 검사합니다.</summary>
        [Test]
        public void PlanFor_InProgress_UsesInjectedFirstRouteDifficulty()
        {
            PlayPlanner planner = new PlayPlanner(ClarityPolicy.Default, RestorePolicy.Default,
                MinigameDifficulty.Hard);

            PlayPlan plan = planner.PlanFor(OwnedRelic.Acquired(RelicId, AcquiredAt), AcquiredAt);

            CollectionAssert.AreEqual(new[] { MinigameDifficulty.Hard }, plan.AllowedDifficulties);
        }

        /// <summary>선명한 정령은 오염 관리 한 판이고 모든 난이도를 고를 수 있는지 검사합니다.</summary>
        [Test]
        public void PlanFor_Clear_GuardOnlyWithAllDifficulties()
        {
            PlayPlan plan = _planner.PlanFor(CreateCompleted(), LastPurifiedAt + TimeSpan.FromDays(3));

            Assert.AreEqual(EntryRoute.Care, plan.Route);
            Assert.AreEqual(Clarity.Clear, plan.Clarity);
            CollectionAssert.AreEqual(new[] { Phase.Guard }, plan.RemainingPhases);
            CollectionAssert.AreEqual(
                new[] { MinigameDifficulty.Easy, MinigameDifficulty.Normal, MinigameDifficulty.Hard },
                plan.AllowedDifficulties);
            Assert.IsTrue(plan.DifficultySelectable);
        }

        /// <summary>흐려진 정령은 기억 회복 한 판이 남고 쉬움을 고를 수 없는지 검사합니다.</summary>
        [Test]
        public void PlanFor_Faded_FromRecallWithoutEasy()
        {
            PlayPlan plan = _planner.PlanFor(CreateCompleted(), LastPurifiedAt + TimeSpan.FromDays(8));

            Assert.AreEqual(EntryRoute.Care, plan.Route);
            Assert.AreEqual(Clarity.Faded, plan.Clarity);
            Assert.AreEqual(Phase.Recall, plan.Phase);
            CollectionAssert.AreEqual(new[] { Phase.Recall }, plan.RemainingPhases);
            CollectionAssert.AreEqual(
                new[] { MinigameDifficulty.Normal, MinigameDifficulty.Hard }, plan.AllowedDifficulties);
        }

        /// <summary>재봉인된 정령은 봉인 해제·기억 회복 두 판이 남고 쉬움을 고를 수 없는지 검사합니다.</summary>
        [Test]
        public void PlanFor_Resealed_FromUnsealWithoutEasy()
        {
            PlayPlan plan = _planner.PlanFor(CreateCompleted(), LastPurifiedAt + TimeSpan.FromDays(15));

            Assert.AreEqual(Phase.Unseal, plan.Phase);
            Assert.AreEqual(Clarity.Resealed, plan.Clarity);
            CollectionAssert.AreEqual(new[] { Phase.Unseal, Phase.Recall }, plan.RemainingPhases);
            CollectionAssert.AreEqual(
                new[] { MinigameDifficulty.Normal, MinigameDifficulty.Hard }, plan.AllowedDifficulties);
        }

        /// <summary>
        /// 선명도는 마지막 정화 시각이 아니라 선명도 기준 시각으로 계산하는지 검사합니다.
        /// 재봉인에서 한 단계를 깨 흐려짐으로 오른 정령(기준 시각 = 마지막 정화 - 7일)은 흐려짐 계획을 받습니다.
        /// </summary>
        [Test]
        public void PlanFor_UsesClarityAnchor()
        {
            OwnedRelic relic = CreateCompleted(LastPurifiedAt - TimeSpan.FromDays(7));

            PlayPlan plan = _planner.PlanFor(relic, LastPurifiedAt);

            Assert.AreEqual(Clarity.Faded, plan.Clarity);
            Assert.AreEqual(Phase.Recall, plan.Phase);
        }

        /// <summary>null 유물·null 정책을 거부하는지 검사합니다.</summary>
        [Test]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => _planner.PlanFor(null, AcquiredAt));
            Assert.Throws<ArgumentNullException>(
                () => new PlayPlanner(null, RestorePolicy.Default, MinigameDifficulty.Normal));
            Assert.Throws<ArgumentNullException>(
                () => new PlayPlanner(ClarityPolicy.Default, null, MinigameDifficulty.Normal));
        }

        /// <summary>정의되지 않은 최초 정화 난이도를 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_UndefinedFirstRouteDifficulty_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new PlayPlanner(ClarityPolicy.Default, RestorePolicy.Default, (MinigameDifficulty)99));
        }
    }
}
