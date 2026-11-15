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
        public void CarePlay_NotCompleted_ThrowsWithoutChange()
        {
            OwnedRelic relic = OwnedRelic.Acquired(RelicId, AcquiredAt);

            Assert.Throws<InvalidOperationException>(() => relic.ClearCarePlay(Phase.Guard, Clarity.Clear, CompletedAt));
            Assert.Throws<InvalidOperationException>(() => relic.RestoreFully(CompletedAt));
            Assert.AreEqual(0, relic.CareCount);
            Assert.IsNull(relic.LastPurifiedAt);
        }

        /// <summary>중간 페이즈를 클리어하면 복구 위치와 시작 선명도만 기록하고 선명도는 회복하지 않는지 검사합니다.</summary>
        [TestCase(Phase.Unseal, Clarity.Resealed)]
        [TestCase(Phase.Recall, Clarity.Resealed)]
        [TestCase(Phase.Recall, Clarity.Faded)]
        public void ClearCarePlay_MiddlePhase_RecordsProgress(Phase phase, Clarity clarity)
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = CompletedAt + TimeSpan.FromDays(20);

            relic.ClearCarePlay(phase, clarity, now);

            Assert.AreEqual(phase, relic.RestorePhase);
            Assert.AreEqual(clarity, relic.RestoreClarity);
            Assert.AreEqual(CompletedAt, relic.LastPurifiedAt);
            Assert.AreEqual(1, relic.CareCount);
        }

        /// <summary>오염 관리를 클리어하면 선명도를 회복하고 복구 진행을 비우는지 검사합니다.</summary>
        [Test]
        public void ClearCarePlay_LastPhase_RestoresClarity()
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = CompletedAt + TimeSpan.FromDays(3);

            relic.ClearCarePlay(Phase.Guard, Clarity.Clear, now);

            Assert.AreEqual(now, relic.LastPurifiedAt);
            Assert.IsNull(relic.RestorePhase);
            Assert.IsNull(relic.RestoreClarity);
            Assert.AreEqual(1, relic.CareCount);
        }

        /// <summary>재봉인 유물을 세 판에 걸쳐 복구하면 판마다 돌봄이 오르고 마지막에 선명도가 회복되는지 검사합니다.</summary>
        [Test]
        public void ClearCarePlay_StepByStepRestore_CountsEveryPlay()
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = CompletedAt + TimeSpan.FromDays(20);

            relic.ClearCarePlay(Phase.Unseal, Clarity.Resealed, now);
            relic.ClearCarePlay(Phase.Recall, Clarity.Resealed, now);
            Assert.AreEqual(Phase.Recall, relic.RestorePhase);
            Assert.AreEqual(CompletedAt, relic.LastPurifiedAt);

            relic.ClearCarePlay(Phase.Guard, Clarity.Resealed, now);

            Assert.AreEqual(3, relic.CareCount);
            Assert.AreEqual(now, relic.LastPurifiedAt);
            Assert.IsNull(relic.RestorePhase);
            Assert.IsNull(relic.RestoreClarity);
        }

        /// <summary>더 나쁜 선명도에서 새로 클리어하면 지난 진행을 덮어쓰는지 검사합니다.</summary>
        [Test]
        public void ClearCarePlay_AfterClarityWorsened_OverwritesProgress()
        {
            OwnedRelic relic = CreateCompleted();
            relic.ClearCarePlay(Phase.Recall, Clarity.Faded, CompletedAt + TimeSpan.FromDays(8));

            relic.ClearCarePlay(Phase.Unseal, Clarity.Resealed, CompletedAt + TimeSpan.FromDays(20));

            Assert.AreEqual(Phase.Unseal, relic.RestorePhase);
            Assert.AreEqual(Clarity.Resealed, relic.RestoreClarity);
            Assert.AreEqual(2, relic.CareCount);
        }

        /// <summary>
        /// 선명도의 복구 순서에 없는 중간 페이즈를 거부하고 상태를 바꾸지 않는지 검사합니다.
        /// (저장 후 생성자가 거부할 상태를 만들지 않기 위해)
        /// </summary>
        [TestCase(Phase.Unseal, Clarity.Clear)]
        [TestCase(Phase.Recall, Clarity.Clear)]
        [TestCase(Phase.Unseal, Clarity.Faded)]
        public void ClearCarePlay_PhaseOutsideRestoreOrder_ThrowsWithoutChange(Phase phase, Clarity clarity)
        {
            OwnedRelic relic = CreateCompleted();

            Assert.Throws<ArgumentException>(() => relic.ClearCarePlay(phase, clarity, CompletedAt + TimeSpan.FromDays(8)));
            Assert.AreEqual(0, relic.CareCount);
            Assert.IsNull(relic.RestorePhase);
            Assert.IsNull(relic.RestoreClarity);
            Assert.AreEqual(CompletedAt, relic.LastPurifiedAt);
        }

        /// <summary>정의되지 않은 열거형을 거부하고 상태를 바꾸지 않는지 검사합니다.</summary>
        [Test]
        public void ClearCarePlay_UndefinedEnum_ThrowsWithoutChange()
        {
            OwnedRelic relic = CreateCompleted();

            Assert.Throws<ArgumentException>(() => relic.ClearCarePlay((Phase)9, Clarity.Resealed, CompletedAt));
            Assert.Throws<ArgumentException>(() => relic.ClearCarePlay(Phase.Recall, (Clarity)9, CompletedAt));
            Assert.AreEqual(0, relic.CareCount);
            Assert.IsNull(relic.RestorePhase);
        }

        /// <summary>오염 관리는 어느 선명도에서든 마지막 판이라 선명도를 회복하는지 검사합니다.</summary>
        [TestCase(Clarity.Clear)]
        [TestCase(Clarity.Faded)]
        [TestCase(Clarity.Resealed)]
        public void ClearCarePlay_Guard_RestoresAtAnyClarity(Clarity clarity)
        {
            OwnedRelic relic = CreateCompleted();
            DateTime now = CompletedAt + TimeSpan.FromDays(20);

            relic.ClearCarePlay(Phase.Guard, clarity, now);

            Assert.AreEqual(now, relic.LastPurifiedAt);
            Assert.AreEqual(1, relic.CareCount);
        }

        /// <summary>전체 복구는 진행 중이던 복구와 관계없이 바로 선명도를 회복하고 돌봄을 1 올리는지 검사합니다.</summary>
        [Test]
        public void RestoreFully_RestoresAndCountsOnce()
        {
            OwnedRelic relic = CreateCompleted();
            relic.ClearCarePlay(Phase.Unseal, Clarity.Resealed, CompletedAt + TimeSpan.FromDays(20));
            DateTime now = CompletedAt + TimeSpan.FromDays(21);

            relic.RestoreFully(now);

            Assert.AreEqual(now, relic.LastPurifiedAt);
            Assert.IsNull(relic.RestorePhase);
            Assert.IsNull(relic.RestoreClarity);
            Assert.AreEqual(2, relic.CareCount);
        }

        /// <summary>시작한 선명도와 지금 선명도가 같을 때만 복구 진행을 인정하는지 검사합니다.</summary>
        [Test]
        public void RestorePhaseFor_AcceptsOnlySameClarity()
        {
            OwnedRelic relic = CreateCompleted();
            relic.ClearCarePlay(Phase.Recall, Clarity.Faded, CompletedAt + TimeSpan.FromDays(8));

            Assert.AreEqual(Phase.Recall, relic.RestorePhaseFor(Clarity.Faded));
            // 복구 도중 재봉인됨 → 처음부터
            Assert.IsNull(relic.RestorePhaseFor(Clarity.Resealed));
            // 기기 시계를 과거로 돌려 선명으로 계산됨 → 진행을 끼워 넣지 않음
            Assert.IsNull(relic.RestorePhaseFor(Clarity.Clear));
        }

        /// <summary>복구 중이 아니면 어떤 선명도에서도 null인지 검사합니다.</summary>
        [TestCase(Clarity.Clear)]
        [TestCase(Clarity.Faded)]
        [TestCase(Clarity.Resealed)]
        public void RestorePhaseFor_NoProgress_IsNull(Clarity clarity)
        {
            Assert.IsNull(CreateCompleted().RestorePhaseFor(clarity));
        }

        /// <summary>정상 조합의 저장 상태로 다시 만들 수 있는지 검사합니다.</summary>
        [Test]
        public void Constructor_ValidCompletedState_Restores()
        {
            OwnedRelic relic = new OwnedRelic(RelicId, AcquiredAt, RelicStatus.Completed, Phase.Guard,
                CompletedAt, Phase.Recall, Clarity.Resealed, 5);

            Assert.IsTrue(relic.IsCompleted);
            Assert.AreEqual(Phase.Recall, relic.RestorePhase);
            Assert.AreEqual(Clarity.Resealed, relic.RestoreClarity);
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
                RelicStatus.Completed, Phase.Guard, CompletedAt, null, null, careCount));
        }

        /// <summary>정의되지 않은 열거형 값을 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_UndefinedEnum_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                (RelicStatus)7, Phase.Unseal, null, null, null, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.InProgress, (Phase)9, null, null, null, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, CompletedAt, (Phase)3, Clarity.Resealed, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, CompletedAt, Phase.Unseal, (Clarity)7, 0));
        }

        /// <summary>복구 시작 선명도의 복구 순서에 없는 복구 페이즈를 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_RestorePhaseOutsideRestoreOrder_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, CompletedAt, Phase.Unseal, Clarity.Faded, 0));
            Assert.DoesNotThrow(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, CompletedAt, Phase.Recall, Clarity.Faded, 0));
            Assert.DoesNotThrow(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, CompletedAt, Phase.Unseal, Clarity.Resealed, 0));
        }

        /// <summary>진행 중인 유물에 있으면 안 되는 값을 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_InProgressWithCompletedValues_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.InProgress, Phase.Recall, null, Phase.Unseal, Clarity.Resealed, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.InProgress, Phase.Recall, CompletedAt, null, null, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.InProgress, Phase.Recall, null, null, null, 1));
        }

        /// <summary>완료된 유물에 없으면 안 되는 값과 나올 수 없는 값을 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_InvalidCompletedState_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Recall, CompletedAt, null, null, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, null, null, null, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, CompletedAt, Phase.Guard, Clarity.Resealed, 0));
        }

        /// <summary>복구 위치와 시작 선명도 중 하나만 있거나, 시작 선명도가 선명이면 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_InvalidRestoreProgress_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, CompletedAt, Phase.Unseal, null, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, CompletedAt, null, Clarity.Faded, 0));
            Assert.Throws<ArgumentException>(() => new OwnedRelic(RelicId, AcquiredAt,
                RelicStatus.Completed, Phase.Guard, CompletedAt, Phase.Unseal, Clarity.Clear, 0));
        }
    }
}
