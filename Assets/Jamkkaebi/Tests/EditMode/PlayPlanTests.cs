using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class PlayPlanTests
    {
        private static readonly Phase[] FullOrder = { Phase.Unseal, Phase.Recall, Phase.Guard };
        private static readonly MinigameDifficulty[] NormalOnly = { MinigameDifficulty.Normal };

        /// <summary>이번 페이즈가 남은 순서의 첫 페이즈인지 검사합니다.</summary>
        [Test]
        public void Phase_IsFirstOfRemaining()
        {
            PlayPlan plan = new PlayPlan(EntryRoute.Care, new[] { Phase.Recall, Phase.Guard }, NormalOnly);

            Assert.AreEqual(EntryRoute.Care, plan.Route);
            Assert.AreEqual(Phase.Recall, plan.Phase);
            CollectionAssert.AreEqual(new[] { Phase.Recall, Phase.Guard }, plan.RemainingPhases);
        }

        /// <summary>null·빈 목록을 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_NullOrEmptyLists_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PlayPlan(EntryRoute.First, null, NormalOnly));
            Assert.Throws<ArgumentException>(() => new PlayPlan(EntryRoute.First, new Phase[0], NormalOnly));
            Assert.Throws<ArgumentNullException>(() => new PlayPlan(EntryRoute.First, FullOrder, null));
            Assert.Throws<ArgumentException>(
                () => new PlayPlan(EntryRoute.First, FullOrder, new MinigameDifficulty[0]));
        }

        /// <summary>생성 후 원본 목록을 바꿔도 계획이 바뀌지 않는지(방어적 복사) 검사합니다.</summary>
        [Test]
        public void Constructor_CopiesLists()
        {
            var phases = new List<Phase> { Phase.Recall, Phase.Guard };
            var difficulties = new List<MinigameDifficulty> { MinigameDifficulty.Normal };
            PlayPlan plan = new PlayPlan(EntryRoute.Care, phases, difficulties);

            phases.Clear();
            difficulties.Add(MinigameDifficulty.Hard);

            Assert.AreEqual(2, plan.RemainingPhases.Count);
            Assert.AreEqual(1, plan.AllowedDifficulties.Count);
        }

        /// <summary>난이도가 하나면 고를 수 없고, 여럿이면 고를 수 있는지 검사합니다.</summary>
        [Test]
        public void DifficultySelectable_DependsOnCount()
        {
            PlayPlan fixedPlan = new PlayPlan(EntryRoute.First, FullOrder, NormalOnly);
            PlayPlan selectablePlan = new PlayPlan(EntryRoute.Care, new[] { Phase.Guard },
                new[] { MinigameDifficulty.Normal, MinigameDifficulty.Hard });

            Assert.IsFalse(fixedPlan.DifficultySelectable);
            Assert.IsTrue(selectablePlan.DifficultySelectable);
        }

        /// <summary>목록에 있는 난이도만 허용하는지 검사합니다.</summary>
        [Test]
        public void Allows_OnlyListedDifficulties()
        {
            PlayPlan plan = new PlayPlan(EntryRoute.Care, new[] { Phase.Guard },
                new[] { MinigameDifficulty.Normal, MinigameDifficulty.Hard });

            Assert.IsTrue(plan.Allows(MinigameDifficulty.Normal));
            Assert.IsTrue(plan.Allows(MinigameDifficulty.Hard));
            Assert.IsFalse(plan.Allows(MinigameDifficulty.Easy));
        }
    }
}
