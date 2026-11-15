using System;
using System.Collections.Generic;
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

        /// <summary>재봉인 전이면 마지막 정화 + 재봉인 시간을 돌려주는지 검사합니다.</summary>
        [TestCase(0)]
        [TestCase(10)]
        public void ResealAt_BeforeResealed_ReturnsResealTime(int elapsedDays)
        {
            DateTime? resealAt = _policy.ResealAt(LastPurifiedAt, LastPurifiedAt + TimeSpan.FromDays(elapsedDays));

            Assert.AreEqual(LastPurifiedAt + TimeSpan.FromDays(14), resealAt);
        }

        /// <summary>이미 재봉인됐으면 null을 돌려주는지 검사합니다.</summary>
        [Test]
        public void ResealAt_AlreadyResealed_ReturnsNull()
        {
            Assert.IsNull(_policy.ResealAt(LastPurifiedAt, LastPurifiedAt + TimeSpan.FromDays(14)));
        }

        /// <summary>복구를 시작하지 않았으면 선명도의 복원 시작 페이즈부터 끝까지 남는지 검사합니다.</summary>
        [Test]
        public void RemainingPhases_NothingCleared_StartsFromRestorePhase()
        {
            CollectionAssert.AreEqual(new[] { Phase.Guard }, _policy.RemainingPhases(Clarity.Clear, null));
            CollectionAssert.AreEqual(new[] { Phase.Recall, Phase.Guard }, _policy.RemainingPhases(Clarity.Faded, null));
            CollectionAssert.AreEqual(new[] { Phase.Unseal, Phase.Recall, Phase.Guard }, _policy.RemainingPhases(Clarity.Resealed, null));
        }

        /// <summary>이번 복구에서 이미 클리어한 페이즈까지는 빠지는지 검사합니다.</summary>
        [TestCase(Clarity.Resealed, Phase.Unseal, new[] { Phase.Recall, Phase.Guard })]
        [TestCase(Clarity.Resealed, Phase.Recall, new[] { Phase.Guard })]
        [TestCase(Clarity.Faded, Phase.Recall, new[] { Phase.Guard })]
        public void RemainingPhases_PartlyCleared_SkipsClearedPhases(Clarity clarity, Phase lastCleared, Phase[] expected)
        {
            CollectionAssert.AreEqual(expected, _policy.RemainingPhases(clarity, lastCleared));
        }

        /// <summary>마지막 페이즈까지 클리어했으면 빈 목록인지 검사합니다.</summary>
        [TestCase(Clarity.Clear)]
        [TestCase(Clarity.Faded)]
        [TestCase(Clarity.Resealed)]
        public void RemainingPhases_LastPhaseCleared_IsEmpty(Clarity clarity)
        {
            IReadOnlyList<Phase> remaining = _policy.RemainingPhases(clarity, Phase.Guard);

            Assert.IsEmpty(remaining);
        }
    }
}
