using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class RestorePolicyTests
    {
        /// <summary>기본 정책이 기획 확정값(쉬움 회복만, 보통 한 단계, 어려움 전체)인지 검사합니다.</summary>
        [TestCase(MinigameDifficulty.Easy, RestoreEffect.RefreshOnly)]
        [TestCase(MinigameDifficulty.Normal, RestoreEffect.OneStep)]
        [TestCase(MinigameDifficulty.Hard, RestoreEffect.Full)]
        public void Default_EffectOf_MatchesPlan(MinigameDifficulty difficulty, RestoreEffect expected)
        {
            Assert.AreEqual(expected, RestorePolicy.Default.EffectOf(difficulty));
        }

        /// <summary>null·빈 대응표를 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_NullOrEmpty_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RestorePolicy(null));
            Assert.Throws<ArgumentException>(
                () => new RestorePolicy(new Dictionary<MinigameDifficulty, RestoreEffect>()));
        }

        /// <summary>정의되지 않은 난이도·복구 효과를 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_UndefinedKeyOrValue_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RestorePolicy(new Dictionary<MinigameDifficulty, RestoreEffect>
            {
                { MinigameDifficulty.Normal, RestoreEffect.OneStep },
                { (MinigameDifficulty)99, RestoreEffect.Full }
            }));
            Assert.Throws<ArgumentException>(() => new RestorePolicy(new Dictionary<MinigameDifficulty, RestoreEffect>
            {
                { MinigameDifficulty.Normal, (RestoreEffect)99 }
            }));
        }

        /// <summary>생성 후 원본 대응표를 바꿔도 정책이 바뀌지 않는지(방어적 복사) 검사합니다.</summary>
        [Test]
        public void Constructor_CopiesSource()
        {
            var source = new Dictionary<MinigameDifficulty, RestoreEffect>
            {
                { MinigameDifficulty.Normal, RestoreEffect.OneStep }
            };
            RestorePolicy policy = new RestorePolicy(source);

            source[MinigameDifficulty.Normal] = RestoreEffect.Full;
            source[MinigameDifficulty.Hard] = RestoreEffect.Full;

            Assert.AreEqual(RestoreEffect.OneStep, policy.EffectOf(MinigameDifficulty.Normal));
            CollectionAssert.AreEqual(new[] { MinigameDifficulty.Normal }, policy.AllowedFor(Clarity.Clear));
        }

        /// <summary>열거형에는 있지만 대응표에 없는 난이도, 정의되지 않은 값을 거부하는지 검사합니다.</summary>
        [Test]
        public void EffectOf_UnregisteredDifficulty_Throws()
        {
            RestorePolicy policy = new RestorePolicy(new Dictionary<MinigameDifficulty, RestoreEffect>
            {
                { MinigameDifficulty.Normal, RestoreEffect.OneStep }
            });

            // ArgumentOutOfRangeException도 ArgumentException을 상속하므로 Catch로 함께 받는다
            Assert.Catch<ArgumentException>(() => policy.EffectOf(MinigameDifficulty.Hard));
            Assert.Catch<ArgumentException>(() => policy.EffectOf((MinigameDifficulty)99));
        }

        /// <summary>선명하면 등록된 난이도를 모두 쉬운 순으로 돌려주는지 검사합니다.</summary>
        [Test]
        public void AllowedFor_Clear_ReturnsAllInOrder()
        {
            CollectionAssert.AreEqual(
                new[] { MinigameDifficulty.Easy, MinigameDifficulty.Normal, MinigameDifficulty.Hard },
                RestorePolicy.Default.AllowedFor(Clarity.Clear));
        }

        /// <summary>흐려짐·재봉인이면 선명도 회복만 하는 난이도를 빼는지 검사합니다.</summary>
        [TestCase(Clarity.Faded)]
        [TestCase(Clarity.Resealed)]
        public void AllowedFor_NotClear_ExcludesRefreshOnly(Clarity clarity)
        {
            CollectionAssert.AreEqual(
                new[] { MinigameDifficulty.Normal, MinigameDifficulty.Hard },
                RestorePolicy.Default.AllowedFor(clarity));
        }

        /// <summary>대응표에 넣은 순서와 관계없이 쉬운 순으로 정렬하는지 검사합니다.</summary>
        [Test]
        public void AllowedFor_IgnoresInsertionOrder()
        {
            RestorePolicy policy = new RestorePolicy(new Dictionary<MinigameDifficulty, RestoreEffect>
            {
                { MinigameDifficulty.Hard, RestoreEffect.Full },
                { MinigameDifficulty.Easy, RestoreEffect.RefreshOnly },
                { MinigameDifficulty.Normal, RestoreEffect.OneStep }
            });

            CollectionAssert.AreEqual(
                new[] { MinigameDifficulty.Easy, MinigameDifficulty.Normal, MinigameDifficulty.Hard },
                policy.AllowedFor(Clarity.Clear));
        }
    }
}
