using System;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Relic;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class OwnedRelicTests
    {
        private const int RelicId = 2;
        private static readonly DateTime AcquiredAt = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime CompletedAt = AcquiredAt + TimeSpan.FromHours(1);

        /// <summary>최초 정화를 세 판 모두 마친 유물을 만든다.</summary>
        private static OwnedRelic CreateCompleted()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);
            relic.ClearFirstPlay(AcquiredAt);
            relic.ClearFirstPlay(AcquiredAt);
            relic.ClearFirstPlay(CompletedAt);
            return relic;
        }

        /// <summary>새로 획득한 유물이 봉인 해제·실루엣·돌봄 0에서 시작하는지 검사합니다.</summary>
        [Test]
        public void Acquired_StartsAtUnsealAsSilhouette()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);

            Assert.AreEqual(RelicId, relic.RelicId);
            Assert.AreEqual(AcquiredAt, relic.AcquiredAt);
            Assert.AreEqual(RelicStatus.InProgress, relic.Status);
            Assert.AreEqual(Phase.Unseal, relic.CurrentPhase);
            Assert.AreEqual(RevealLevel.Silhouette, relic.RevealLevel);
            Assert.IsNull(relic.LastPurifiedAt);
            Assert.IsNull(relic.RestorePhase);
            Assert.AreEqual(0, relic.CareCount);
            Assert.IsFalse(relic.IsCompleted);
            Assert.IsFalse(relic.ShowsSpiritName);
        }

        /// <summary>최초 정화가 봉인 해제 → 기억 회복 → 오염 관리 순서로 진행하고 공개 단계가 따라 바뀌는지 검사합니다.</summary>
        [Test]
        public void ClearFirstPlay_AdvancesPhaseAndRevealLevel()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);

            Assert.IsFalse(relic.ClearFirstPlay(AcquiredAt));
            Assert.AreEqual(Phase.Recall, relic.CurrentPhase);
            Assert.AreEqual(RevealLevel.Form, relic.RevealLevel);

            Assert.IsFalse(relic.ClearFirstPlay(AcquiredAt));
            Assert.AreEqual(Phase.Guard, relic.CurrentPhase);
            Assert.AreEqual(RevealLevel.Named, relic.RevealLevel);
            // 기억 회복을 마쳐 실제 이름은 보이지만 정령 이름은 아직이다
            Assert.IsTrue(relic.RevealLevel.ShowsRealName());
            Assert.IsFalse(relic.ShowsSpiritName);
            Assert.AreEqual(RelicStatus.InProgress, relic.Status);
        }

        /// <summary>마지막 페이즈를 마치면 true를 돌려주고 완료·마지막 정화 시각이 정해지는지 검사합니다.</summary>
        [Test]
        public void ClearFirstPlay_LastPhase_Completes()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);
            relic.ClearFirstPlay(AcquiredAt);
            relic.ClearFirstPlay(AcquiredAt);

            bool completedNow = relic.ClearFirstPlay(CompletedAt);

            Assert.IsTrue(completedNow);
            Assert.IsTrue(relic.IsCompleted);
            Assert.IsTrue(relic.ShowsSpiritName);
            Assert.AreEqual(Phase.Guard, relic.CurrentPhase);
            Assert.AreEqual(RevealLevel.Named, relic.RevealLevel);
            Assert.AreEqual(CompletedAt, relic.LastPurifiedAt);
            Assert.IsNull(relic.RestorePhase);
            Assert.AreEqual(0, relic.CareCount);
        }

        /// <summary>완료된 유물에 최초 정화를 또 부르면 거부하는지 검사합니다.</summary>
        [Test]
        public void ClearFirstPlay_AlreadyCompleted_Throws()
        {
            OwnedRelic relic = CreateCompleted();

            Assert.Throws<InvalidOperationException>(() => relic.ClearFirstPlay(CompletedAt));
        }

        /// <summary>완료되지 않은 유물의 관리 플레이를 거부하고 상태를 바꾸지 않는지 검사합니다.</summary>
        [Test]
        public void ClearCarePlay_NotCompleted_ThrowsWithoutChange()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);

            Assert.Throws<InvalidOperationException>(() => relic.ClearCarePlay(Phase.Guard, CompletedAt));
            Assert.AreEqual(0, relic.CareCount);
            Assert.IsNull(relic.LastPurifiedAt);
        }

        /// <summary>중간 페이즈를 클리어하면 복구 위치만 기록하고 선명도는 회복하지 않는지 검사합니다.</summary>
        [TestCase(Phase.Unseal)]
        [TestCase(Phase.Recall)]
        public void ClearCarePlay_MiddlePhase_RecordsRestorePhase(Phase phase)
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = CompletedAt + TimeSpan.FromDays(20);

            relic.ClearCarePlay(phase, now);

            Assert.AreEqual(phase, relic.RestorePhase);
            Assert.AreEqual(CompletedAt, relic.LastPurifiedAt);
            Assert.AreEqual(1, relic.CareCount);
        }

        /// <summary>오염 관리를 클리어하면 선명도를 회복하고 복구 위치를 비우는지 검사합니다.</summary>
        [Test]
        public void ClearCarePlay_LastPhase_RestoresClarity()
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = CompletedAt + TimeSpan.FromDays(3);

            relic.ClearCarePlay(Phase.Guard, now);

            Assert.AreEqual(now, relic.LastPurifiedAt);
            Assert.IsNull(relic.RestorePhase);
            Assert.AreEqual(1, relic.CareCount);
        }

        /// <summary>재봉인 유물을 세 판에 걸쳐 복구하면 판마다 돌봄이 오르고 마지막에 선명도가 회복되는지 검사합니다.</summary>
        [Test]
        public void ClearCarePlay_FullRestore_CountsEveryPlay()
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = CompletedAt + TimeSpan.FromDays(20);

            relic.ClearCarePlay(Phase.Unseal, now);
            relic.ClearCarePlay(Phase.Recall, now);
            Assert.AreEqual(Phase.Recall, relic.RestorePhase);
            Assert.AreEqual(CompletedAt, relic.LastPurifiedAt);

            relic.ClearCarePlay(Phase.Guard, now);

            Assert.AreEqual(3, relic.CareCount);
            Assert.AreEqual(now, relic.LastPurifiedAt);
            Assert.IsNull(relic.RestorePhase);
        }

        /// <summary>정상 조합의 저장 상태로 다시 만들 수 있는지 검사합니다.</summary>
        [Test]
        public void Constructor_ValidCompletedState_Restores()
        {
            OwnedRelic relic = new OwnedRelic(RelicId, AcquiredAt, RelicStatus.Completed, Phase.Guard,
                CompletedAt, Phase.Recall, 5);

            Assert.IsTrue(relic.IsCompleted);
            Assert.AreEqual(Phase.Recall, relic.RestorePhase);
            Assert.AreEqual(CompletedAt, relic.LastPurifiedAt);
            Assert.AreEqual(5, relic.CareCount);
        }

        /// <summary>범위를 벗어난 relicId·돌봄 횟수를 거부하는지 검사합니다.</summary>
        [TestCase(0, 0)]
        [TestCase(-1, 0)]
        [TestCase(RelicId, -1)]
        public void Constructor_OutOfRange_Throws(int relicId, int careCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OwnedRelic(relicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, CompletedAt, null, careCount));
        }

        /// <summary>정의되지 않은 열거형 값을 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_UndefinedEnum_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                (RelicStatus)7, Phase.Unseal, null, null, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.InProgress, (Phase)9, null, null, 0));
        }

        /// <summary>진행 중인 유물에 있으면 안 되는 값을 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_InProgressWithCompletedValues_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.InProgress, Phase.Recall, null, Phase.Unseal, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.InProgress, Phase.Recall, CompletedAt, null, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.InProgress, Phase.Recall, null, null, 1));
        }

        /// <summary>완료된 유물에 없으면 안 되는 값과 나올 수 없는 값을 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_InvalidCompletedState_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Recall, CompletedAt, null, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, null, null, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, CompletedAt, Phase.Guard, 0));
        }
    }
}
