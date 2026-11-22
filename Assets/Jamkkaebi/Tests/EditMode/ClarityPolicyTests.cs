using System;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Relic;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class ClarityPolicyTests
    {
        private static readonly DateTime LastPurifiedAt = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);

        private ClarityPolicy _policy;

        [SetUp]
        public void SetUp()
        {
            _policy = new ClarityPolicy(TimeSpan.FromDays(7), TimeSpan.FromDays(14));
        }

        /// <summary>기본 정책이 서버 기본값(7일, 14일)을 쓰는지 검사합니다.</summary>
        [Test]
        public void Default_UsesServerDefaults()
        {
            Assert.AreEqual(TimeSpan.FromDays(7), ClarityPolicy.Default.FadedAfter);
            Assert.AreEqual(TimeSpan.FromDays(14), ClarityPolicy.Default.ResealedAfter);
        }

        /// <summary>흐려짐 시간이 0 이하이면 거부하는지 검사합니다.</summary>
        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_NonPositiveFadedAfter_Throws(int fadedDays)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ClarityPolicy(TimeSpan.FromDays(fadedDays), TimeSpan.FromDays(14)));
        }

        /// <summary>재봉인 시간이 흐려짐 시간 이하이면 거부하는지 검사합니다. 같으면 흐려짐 단계가 사라집니다.</summary>
        [TestCase(7)]
        [TestCase(3)]
        public void Constructor_ResealedNotAfterFaded_Throws(int resealedDays)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ClarityPolicy(TimeSpan.FromDays(7), TimeSpan.FromDays(resealedDays)));
        }

        /// <summary>경과 시간에 따라 선명도가 바뀌는 경계를 검사합니다.</summary>
        [Test]
        public void ClarityOf_AtBoundaries_ReturnsExpectedClarity()
        {
            TimeSpan tick = TimeSpan.FromTicks(1);

            Assert.AreEqual(Clarity.Clear, _policy.ClarityOf(LastPurifiedAt, LastPurifiedAt));
            Assert.AreEqual(Clarity.Clear, _policy.ClarityOf(LastPurifiedAt, LastPurifiedAt + TimeSpan.FromDays(7) - tick));
            Assert.AreEqual(Clarity.Faded, _policy.ClarityOf(LastPurifiedAt, LastPurifiedAt + TimeSpan.FromDays(7)));
            Assert.AreEqual(Clarity.Faded, _policy.ClarityOf(LastPurifiedAt, LastPurifiedAt + TimeSpan.FromDays(14) - tick));
            Assert.AreEqual(Clarity.Resealed, _policy.ClarityOf(LastPurifiedAt, LastPurifiedAt + TimeSpan.FromDays(14)));
            Assert.AreEqual(Clarity.Resealed, _policy.ClarityOf(LastPurifiedAt, LastPurifiedAt + TimeSpan.FromDays(100)));
        }

        /// <summary>기기 시각이 마지막 정화보다 앞서 있으면 선명으로 보는지 검사합니다.</summary>
        [Test]
        public void ClarityOf_NowBeforeLastPurified_IsClear()
        {
            Assert.AreEqual(Clarity.Clear, _policy.ClarityOf(LastPurifiedAt, LastPurifiedAt - TimeSpan.FromDays(3)));
        }

        /// <summary>선명이면 흐려지는 시각, 흐려짐이면 재봉인되는 시각을 돌려주는지 검사합니다.</summary>
        [TestCase(0, 7)]
        [TestCase(6, 7)]
        [TestCase(7, 14)]
        [TestCase(13, 14)]
        public void NextChangeAt_BeforeResealed_ReturnsNextDrop(int elapsedDays, int changeDays)
        {
            DateTime? changesAt = _policy.NextChangeAt(LastPurifiedAt, LastPurifiedAt + TimeSpan.FromDays(elapsedDays));

            Assert.AreEqual(LastPurifiedAt + TimeSpan.FromDays(changeDays), changesAt);
        }

        /// <summary>이미 재봉인됐으면 더 떨어질 선명도가 없어 null을 돌려주는지 검사합니다.</summary>
        [Test]
        public void NextChangeAt_AlreadyResealed_ReturnsNull()
        {
            Assert.IsNull(_policy.NextChangeAt(LastPurifiedAt, LastPurifiedAt + TimeSpan.FromDays(14)));
        }

        /// <summary>선명은 지금, 흐려짐은 흐려짐이 막 시작된 시점을 기준 시각으로 돌려주는지 검사합니다.</summary>
        [Test]
        public void AnchorFor_ClearAndFaded()
        {
            DateTime now = LastPurifiedAt + TimeSpan.FromDays(20);

            Assert.AreEqual(now, _policy.AnchorFor(Clarity.Clear, now));
            Assert.AreEqual(now - TimeSpan.FromDays(7), _policy.AnchorFor(Clarity.Faded, now));
        }

        /// <summary>흐려짐으로 오른 기준 시각은 지금 흐려짐이고, 흐려짐 구간을 온전히(7일) 받은 뒤 재봉인되는지 검사합니다.</summary>
        [Test]
        public void AnchorFor_Faded_GivesFullFadedWindow()
        {
            DateTime now = LastPurifiedAt + TimeSpan.FromDays(20);
            DateTime anchor = _policy.AnchorFor(Clarity.Faded, now);

            Assert.AreEqual(Clarity.Faded, _policy.ClarityOf(anchor, now));
            Assert.AreEqual(now + TimeSpan.FromDays(7), _policy.NextChangeAt(anchor, now));
        }

        /// <summary>복구로 재봉인이 될 수는 없어 거부하는지 검사합니다.</summary>
        [Test]
        public void AnchorFor_Resealed_Throws()
        {
            Assert.Throws<ArgumentException>(() => _policy.AnchorFor(Clarity.Resealed, LastPurifiedAt));
            Assert.Throws<ArgumentException>(() => _policy.AnchorFor((Clarity)9, LastPurifiedAt));
        }

        /// <summary>흐려짐으로 막 오른 정령(기준 = 마지막 정화 - 7일).</summary>
        private static OwnedRelic SteppedToFaded() =>
            new OwnedRelic(2, LastPurifiedAt - TimeSpan.FromDays(30), RelicStatus.Completed, Phase.Guard,
                LastPurifiedAt, LastPurifiedAt - TimeSpan.FromDays(7), 1);

        /// <summary>
        /// 기기 시각이 마지막 정화보다 뒤로 가도 막 오른 흐려짐이 선명으로 바뀌지 않고, 다음 변화 시각도 그대로인지 검사합니다.
        /// (기준 시각만으로 계산하면 1초 역행에도 선명이 된다)
        /// </summary>
        [Test]
        public void ClarityOfRelic_ClockRollback_KeepsLastPurifiedState()
        {
            OwnedRelic relic = SteppedToFaded();
            DateTime rolledBack = LastPurifiedAt - TimeSpan.FromSeconds(1);

            Assert.AreEqual(Clarity.Clear, _policy.ClarityOf(relic.ClarityAnchorAt.Value, rolledBack));
            Assert.AreEqual(Clarity.Faded, _policy.ClarityOf(relic, rolledBack));
            Assert.AreEqual(LastPurifiedAt + TimeSpan.FromDays(7), _policy.NextChangeAt(relic, rolledBack));
        }

        /// <summary>시간이 정상으로 흐르면 정령 계산도 기준 시각의 7일·14일 경계를 그대로 따르는지 검사합니다.</summary>
        [Test]
        public void ClarityOfRelic_NormalTime_FollowsAnchorBoundaries()
        {
            OwnedRelic relic = SteppedToFaded();
            TimeSpan tick = TimeSpan.FromTicks(1);

            Assert.AreEqual(Clarity.Faded, _policy.ClarityOf(relic, LastPurifiedAt));
            Assert.AreEqual(Clarity.Faded, _policy.ClarityOf(relic, LastPurifiedAt + TimeSpan.FromDays(7) - tick));
            Assert.AreEqual(Clarity.Resealed, _policy.ClarityOf(relic, LastPurifiedAt + TimeSpan.FromDays(7)));
            Assert.IsNull(_policy.NextChangeAt(relic, LastPurifiedAt + TimeSpan.FromDays(7)));
        }

        /// <summary>최초 정화를 마치지 않은 유물이나 null은 선명도를 계산하지 않고 거부하는지 검사합니다.</summary>
        [Test]
        public void ClarityOfRelic_NotCompletedOrNull_Throws()
        {
            OwnedRelic inProgress = OwnedRelic.Acquired(2, LastPurifiedAt);

            Assert.Throws<InvalidOperationException>(() => _policy.ClarityOf(inProgress, LastPurifiedAt));
            Assert.Throws<InvalidOperationException>(() => _policy.NextChangeAt(inProgress, LastPurifiedAt));
            Assert.Throws<ArgumentNullException>(() => _policy.ClarityOf((OwnedRelic)null, LastPurifiedAt));
        }

        /// <summary>선명까지 남은 판: 재봉인은 봉인 해제·기억 회복, 흐려짐은 기억 회복, 선명은 관리 판(오염 관리) 하나인지 검사합니다.</summary>
        [Test]
        public void RemainingPhases_ByClarity()
        {
            CollectionAssert.AreEqual(new[] { Phase.Guard }, _policy.RemainingPhases(Clarity.Clear));
            CollectionAssert.AreEqual(new[] { Phase.Recall }, _policy.RemainingPhases(Clarity.Faded));
            CollectionAssert.AreEqual(new[] { Phase.Unseal, Phase.Recall }, _policy.RemainingPhases(Clarity.Resealed));
        }

        /// <summary>복구 한 칸 오르기와 시간이 지나 떨어지기의 순서를 검사합니다.</summary>
        [Test]
        public void StepUpAndDown()
        {
            Assert.AreEqual(Clarity.Faded, Clarity.Resealed.StepUp());
            Assert.AreEqual(Clarity.Clear, Clarity.Faded.StepUp());
            Assert.AreEqual(Clarity.Clear, Clarity.Clear.StepUp());

            Assert.AreEqual(Clarity.Faded, Clarity.Clear.StepDown());
            Assert.AreEqual(Clarity.Resealed, Clarity.Faded.StepDown());
            Assert.AreEqual(Clarity.Resealed, Clarity.Resealed.StepDown());
        }
    }
}
