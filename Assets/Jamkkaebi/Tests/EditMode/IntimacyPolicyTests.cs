using System;
using Jamkkaebi.Scripts.Gameplay.Relic;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class IntimacyPolicyTests
    {
        // 누적 돌봄 3회에 2단계, 7회에 3단계(최대)
        private IntimacyPolicy _policy;

        [SetUp]
        public void SetUp()
        {
            _policy = new IntimacyPolicy(new[] { 3, 7 });
        }

        /// <summary>기본 정책을 예외 없이 만들 수 있고 기준값 개수 + 1이 최대 단계인지 검사합니다.</summary>
        [Test]
        public void Default_IsValid()
        {
            IntimacyPolicy policy = null;

            Assert.DoesNotThrow(() => policy = IntimacyPolicy.Default);
            Assert.AreEqual(policy.LevelUpCareCounts.Count + 1, policy.MaxLevel);
        }

        /// <summary>최대 단계가 기준값 개수 + 1인지 검사합니다.</summary>
        [Test]
        public void MaxLevel_IsCountPlusOne()
        {
            Assert.AreEqual(3, _policy.MaxLevel);
        }

        /// <summary>null 기준값을 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new IntimacyPolicy(null));
        }

        /// <summary>빈 기준값, 1 미만 기준값, 오름차순이 아닌 기준값을 거부하는지 검사합니다.</summary>
        [TestCase(new int[0])]
        [TestCase(new[] { 0, 3 })]
        [TestCase(new[] { -1, 3 })]
        [TestCase(new[] { 3, 3 })]
        [TestCase(new[] { 7, 3 })]
        [TestCase(new[] { 3, 7, 5 })]
        public void Constructor_InvalidCounts_Throws(int[] counts)
        {
            // ArgumentOutOfRangeException도 ArgumentException을 상속하므로 Catch로 함께 받는다
            Assert.Catch<ArgumentException>(() => new IntimacyPolicy(counts));
        }

        /// <summary>생성 후 원본 배열을 바꿔도 정책이 바뀌지 않는지(방어적 복사) 검사합니다.</summary>
        [Test]
        public void Constructor_CopiesSource()
        {
            int[] source = { 3, 7 };
            IntimacyPolicy policy = new IntimacyPolicy(source);

            source[0] = 100;

            Assert.AreEqual(3, policy.LevelUpCareCounts[0]);
        }

        /// <summary>돌봄 횟수에 따른 단계, 단계 안 진행 횟수, 다음 단계까지 필요한 횟수를 검사합니다.</summary>
        [TestCase(0, 1, 0, 3)]
        [TestCase(2, 1, 2, 3)]
        [TestCase(3, 2, 0, 4)]
        [TestCase(6, 2, 3, 4)]
        public void GaugeOf_BelowMax_ReturnsLevelAndProgress(int careCount, int level, int current, int max)
        {
            IntimacyGauge gauge = _policy.GaugeOf(careCount);

            Assert.AreEqual(level, gauge.Level);
            Assert.AreEqual(current, gauge.Current);
            Assert.AreEqual(max, gauge.Max);
        }

        /// <summary>단계 안에서 채움 비율이 진행 횟수 / 필요 횟수인지 검사합니다.</summary>
        [Test]
        public void GaugeOf_BelowMax_FillIsRatio()
        {
            Assert.AreEqual(0f, _policy.GaugeOf(0).Fill, 1e-5f);
            Assert.AreEqual(2f / 3f, _policy.GaugeOf(2).Fill, 1e-5f);
            Assert.AreEqual(0.75f, _policy.GaugeOf(6).Fill, 1e-5f);
        }

        /// <summary>마지막 기준값 이상이면 최대 단계이고 게이지가 가득 찬 것으로 보는지 검사합니다.</summary>
        [TestCase(7)]
        [TestCase(100)]
        public void GaugeOf_AtOrAboveLastCount_IsMax(int careCount)
        {
            IntimacyGauge gauge = _policy.GaugeOf(careCount);

            Assert.AreEqual(_policy.MaxLevel, gauge.Level);
            Assert.AreEqual(0, gauge.Max);
            Assert.AreEqual(1f, gauge.Fill);
        }

        /// <summary>음수 돌봄 횟수를 거부하는지 검사합니다.</summary>
        [Test]
        public void GaugeOf_NegativeCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _policy.GaugeOf(-1));
        }
    }
}
