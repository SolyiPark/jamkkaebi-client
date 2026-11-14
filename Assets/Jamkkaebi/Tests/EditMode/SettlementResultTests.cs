using System;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class SettlementResultTests
    {
        private static readonly PlayPlan NextPlan = new PlayPlan(EntryRoute.First,
            new[] { Phase.Recall, Phase.Guard }, new[] { MinigameDifficulty.Normal }, null);

        /// <summary>정상 조합으로 만들 수 있고 값이 그대로 담기는지 검사합니다.</summary>
        [Test]
        public void Constructor_ValidCombinations_Keep()
        {
            SettlementResult nextPhase = new SettlementResult(PlayResult.Success, NextAction.NextPhase,
                NextPlan, false, RevealLevel.Form);
            SettlementResult done = new SettlementResult(PlayResult.Success, NextAction.Done,
                null, true, RevealLevel.Named);
            SettlementResult retry = new SettlementResult(PlayResult.Failure, NextAction.Retry,
                NextPlan, false, null);

            Assert.AreEqual(NextAction.NextPhase, nextPhase.Next);
            Assert.AreSame(NextPlan, nextPhase.NextPlan);
            Assert.AreEqual(RevealLevel.Form, nextPhase.Revealed);
            Assert.IsTrue(done.CompletedNow);
            Assert.IsNull(done.NextPlan);
            Assert.AreEqual(PlayResult.Failure, retry.Result);
            Assert.IsNull(retry.Revealed);
        }

        /// <summary>Done이면 다음 계획이 없어야 하고, 아니면 있어야 하는지 검사합니다.</summary>
        [Test]
        public void Constructor_NextPlanMismatchWithDone_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SettlementResult(PlayResult.Success, NextAction.Done,
                NextPlan, false, null));
            Assert.Throws<ArgumentException>(() => new SettlementResult(PlayResult.Success, NextAction.ContinueRestore,
                null, false, null));
        }

        /// <summary>실패면 Retry여야 하는지 검사합니다.</summary>
        [Test]
        public void Constructor_FailureWithoutRetry_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SettlementResult(PlayResult.Failure, NextAction.NextPhase,
                NextPlan, false, null));
        }

        /// <summary>이번에 최초 정화를 마쳤으면 Done이어야 하는지 검사합니다.</summary>
        [Test]
        public void Constructor_CompletedNowWithoutDone_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SettlementResult(PlayResult.Success, NextAction.NextPhase,
                NextPlan, true, RevealLevel.Named));
        }
    }
}
