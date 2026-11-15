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

        /// <summary>최초 정화를 마친 유물. 복구 진행 위치와 그 복구를 시작한 선명도를 줄 수 있다.</summary>
        private static OwnedRelic CreateCompleted(Phase? restorePhase = null, Clarity? restoreClarity = null)
        {
            return new OwnedRelic(RelicId, AcquiredAt, RelicStatus.Completed, Phase.Guard,
                LastPurifiedAt, restorePhase, restoreClarity, 0);
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

        /// <summary>흐려진 정령은 기억 회복부터이고 쉬움을 고를 수 없는지 검사합니다.</summary>
        [Test]
        public void PlanFor_Faded_FromRecallWithoutEasy()
        {
            PlayPlan plan = _planner.PlanFor(CreateCompleted(), LastPurifiedAt + TimeSpan.FromDays(8));

            Assert.AreEqual(EntryRoute.Care, plan.Route);
            Assert.AreEqual(Clarity.Faded, plan.Clarity);
            CollectionAssert.AreEqual(new[] { Phase.Recall, Phase.Guard }, plan.RemainingPhases);
            CollectionAssert.AreEqual(
                new[] { MinigameDifficulty.Normal, MinigameDifficulty.Hard }, plan.AllowedDifficulties);
        }

        /// <summary>재봉인된 정령은 봉인 해제부터이고 쉬움을 고를 수 없는지 검사합니다.</summary>
        [Test]
        public void PlanFor_Resealed_FromUnsealWithoutEasy()
        {
            PlayPlan plan = _planner.PlanFor(CreateCompleted(), LastPurifiedAt + TimeSpan.FromDays(15));

            Assert.AreEqual(Phase.Unseal, plan.Phase);
            Assert.AreEqual(Clarity.Resealed, plan.Clarity);
            CollectionAssert.AreEqual(new[] { Phase.Unseal, Phase.Recall, Phase.Guard }, plan.RemainingPhases);
            CollectionAssert.AreEqual(
                new[] { MinigameDifficulty.Normal, MinigameDifficulty.Hard }, plan.AllowedDifficulties);
        }

        /// <summary>복구 도중이면 이미 클리어한 페이즈를 건너뛰는지 검사합니다.</summary>
        [Test]
        public void PlanFor_ResealedMidRestore_SkipsClearedPhase()
        {
            PlayPlan plan = _planner.PlanFor(CreateCompleted(Phase.Unseal, Clarity.Resealed),
                LastPurifiedAt + TimeSpan.FromDays(15));

            Assert.AreEqual(Phase.Recall, plan.Phase);
            CollectionAssert.AreEqual(new[] { Phase.Recall, Phase.Guard }, plan.RemainingPhases);
        }

        /// <summary>
        /// 흐려짐에서 기억 회복까지 복구한 뒤 재봉인되면, 지난 진행을 버리고 봉인 해제부터 다시 하는지 검사합니다.
        /// (서버 수정 요청 7번: 서버는 오염 관리만 남겨 두 페이즈를 건너뛴다)
        /// </summary>
        [Test]
        public void PlanFor_ResealedAfterFadedProgress_RestartsFromUnseal()
        {
            PlayPlan plan = _planner.PlanFor(CreateCompleted(Phase.Recall, Clarity.Faded),
                LastPurifiedAt + TimeSpan.FromDays(15));

            Assert.AreEqual(Phase.Unseal, plan.Phase);
            CollectionAssert.AreEqual(new[] { Phase.Unseal, Phase.Recall, Phase.Guard }, plan.RemainingPhases);
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
