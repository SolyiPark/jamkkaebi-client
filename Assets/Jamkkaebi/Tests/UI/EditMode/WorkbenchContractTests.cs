using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;
using Jamkkaebi.Scripts.UI.Workbench;
using NUnit.Framework;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>작업대 백엔드 응답 그릇과 IWorkbenchBackend 계약을 검사합니다.</summary>
    public sealed class WorkbenchContractTests
    {
        private static readonly DateTime ResealAt = new DateTime(2026, 11, 30, 9, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime LastPurifiedAt = ResealAt - TimeSpan.FromDays(14);

        private static readonly PlayPlan FirstPlan = new PlayPlan(EntryRoute.First,
            new[] { Phase.Unseal, Phase.Recall, Phase.Guard }, new[] { MinigameDifficulty.Normal }, null);

        private static readonly PlayPlan CarePlan = new PlayPlan(EntryRoute.Care,
            new[] { Phase.Recall, Phase.Guard }, new[] { MinigameDifficulty.Normal }, Clarity.Faded);

        private static WorkbenchSlot Slot(int relicId) =>
            new WorkbenchSlot(relicId, RevealLevel.Silhouette, Phase.Unseal);

        private static WorkbenchSnapshot Snapshot(int capacity, params WorkbenchSlot[] slots) =>
            new WorkbenchSnapshot(capacity, slots, new WorkbenchSpirit[0], 10);

        // ---------- WorkbenchSlot ----------

        /// <summary>올바른 값이면 그대로 담는지 검사합니다.</summary>
        [Test]
        public void Slot_StoresValues()
        {
            WorkbenchSlot slot = new WorkbenchSlot(4, RevealLevel.Form, Phase.Recall);

            Assert.AreEqual(4, slot.RelicId);
            Assert.AreEqual(RevealLevel.Form, slot.RevealLevel);
            Assert.AreEqual(Phase.Recall, slot.CurrentPhase);
        }

        /// <summary>relicId가 1 미만이거나 열거형 값이 정의되지 않았으면 거부하는지 검사합니다.</summary>
        [Test]
        public void Slot_InvalidValues_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorkbenchSlot(0, RevealLevel.Silhouette, Phase.Unseal));
            Assert.Throws<ArgumentException>(() => new WorkbenchSlot(1, (RevealLevel)99, Phase.Unseal));
            Assert.Throws<ArgumentException>(() => new WorkbenchSlot(1, RevealLevel.Silhouette, (Phase)99));
        }

        // ---------- WorkbenchSpirit ----------

        /// <summary>올바른 값이면 그대로 담고, 잘못된 값은 거부하는지 검사합니다.</summary>
        [Test]
        public void Spirit_ValidatesValues()
        {
            WorkbenchSpirit spirit = new WorkbenchSpirit(2, "정령2", Clarity.Resealed, 0);
            Assert.AreEqual(2, spirit.RelicId);
            Assert.AreEqual(Clarity.Resealed, spirit.Clarity);
            Assert.AreEqual(0, spirit.CareCount);

            Assert.AreEqual("정령2", spirit.SpiritName);
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorkbenchSpirit(0, "정령0", Clarity.Clear, 0));
            Assert.Throws<ArgumentException>(() => new WorkbenchSpirit(1, null, Clarity.Clear, 0));
            Assert.Throws<ArgumentException>(() => new WorkbenchSpirit(1, "", Clarity.Clear, 0));
            Assert.Throws<ArgumentException>(() => new WorkbenchSpirit(1, "정령1", (Clarity)99, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorkbenchSpirit(1, "정령1", Clarity.Clear, -1));
        }

        // ---------- WorkbenchSnapshot ----------

        /// <summary>null 목록, 용량 1 미만, 음수 미획득 수를 거부하는지 검사합니다.</summary>
        [Test]
        public void Snapshot_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new WorkbenchSnapshot(3, null, new WorkbenchSpirit[0], 0));
            Assert.Throws<ArgumentNullException>(() => new WorkbenchSnapshot(3, new WorkbenchSlot[0], null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Snapshot(0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorkbenchSnapshot(3, new WorkbenchSlot[0], new WorkbenchSpirit[0], -1));
        }

        /// <summary>슬롯·정령 목록 안에 null이 있으면 ArgumentException으로 거부하는지 검사합니다.</summary>
        [Test]
        public void Snapshot_NullElement_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new WorkbenchSnapshot(3, new[] { Slot(1), null }, new WorkbenchSpirit[0], 0));
            Assert.Throws<ArgumentException>(() =>
                new WorkbenchSnapshot(3, new WorkbenchSlot[0], new WorkbenchSpirit[] { null }, 0));
        }

        /// <summary>슬롯 수가 용량보다 적으면 가득 차지 않은 것으로 보는지 검사합니다.</summary>
        [Test]
        public void Snapshot_IsFull_FalseBelowCapacity()
        {
            Assert.IsFalse(Snapshot(3).IsFull);
            Assert.IsFalse(Snapshot(3, Slot(1), Slot(2)).IsFull);
        }

        /// <summary>슬롯 수가 용량과 같으면 가득 찬 것으로 보는지 검사합니다.</summary>
        [Test]
        public void Snapshot_IsFull_TrueAtCapacity()
        {
            Assert.IsTrue(Snapshot(2, Slot(1), Slot(2)).IsFull);
        }

        /// <summary>서버 용량이 줄어 슬롯이 용량보다 많아도 받아들이고 가득 찬 것으로 보는지 검사합니다.</summary>
        [Test]
        public void Snapshot_OverCapacity_AcceptedAndFull()
        {
            WorkbenchSnapshot snapshot = Snapshot(2, Slot(1), Slot(2), Slot(3));

            Assert.AreEqual(3, snapshot.Slots.Count);
            Assert.IsTrue(snapshot.IsFull);
        }

        /// <summary>생성 후 원본 배열을 바꿔도 목록이 바뀌지 않고, 순서를 그대로 지키는지 검사합니다.</summary>
        [Test]
        public void Snapshot_CopiesListsInOrder()
        {
            WorkbenchSlot[] slots = { Slot(7), Slot(2) };
            WorkbenchSpirit[] spirits = { new WorkbenchSpirit(5, "정령5", Clarity.Resealed, 1), new WorkbenchSpirit(3, "정령3", Clarity.Clear, 0) };
            WorkbenchSnapshot snapshot = new WorkbenchSnapshot(3, slots, spirits, 4);

            slots[0] = Slot(9);
            spirits[0] = new WorkbenchSpirit(9, "정령9", Clarity.Clear, 0);

            CollectionAssert.AreEqual(new[] { 7, 2 }, snapshot.Slots.Select(s => s.RelicId));
            CollectionAssert.AreEqual(new[] { 5, 3 }, snapshot.Spirits.Select(s => s.RelicId));
            Assert.AreEqual(3, snapshot.Capacity);
            Assert.AreEqual(4, snapshot.LockedCount);
        }

        /// <summary>돌려준 목록을 바깥에서 바꿀 수 없는지 검사합니다.</summary>
        [Test]
        public void Snapshot_ListsAreReadOnly()
        {
            WorkbenchSnapshot snapshot = Snapshot(3, Slot(1));

            Assert.Throws<NotSupportedException>(() => ((IList<WorkbenchSlot>)snapshot.Slots).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<WorkbenchSpirit>)snapshot.Spirits).Clear());
        }

        // ---------- RelicDetail ----------

        /// <summary>최초 정화 중인 유물은 선명도·재봉인 시각 없이, 돌봄 0회로 만들어지는지 검사합니다.</summary>
        [Test]
        public void Detail_InProgress_WithoutClarity()
        {
            RelicDetail detail = new RelicDetail(1, RelicStatus.InProgress, null, null, null, null, 0, FirstPlan);

            Assert.AreEqual(1, detail.RelicId);
            Assert.AreEqual(RelicStatus.InProgress, detail.Status);
            Assert.IsNull(detail.Clarity);
            Assert.IsNull(detail.ResealAt);
            Assert.AreEqual(0, detail.CareCount);
            Assert.AreSame(FirstPlan, detail.NextPlay);
        }

        /// <summary>최초 정화 중인 유물에 선명도·재봉인 시각·돌봄 횟수가 있으면 거부하는지 검사합니다.</summary>
        [Test]
        public void Detail_InProgress_WithCompletedFields_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new RelicDetail(1, RelicStatus.InProgress, null, Clarity.Clear, null, null, 0, FirstPlan));
            Assert.Throws<ArgumentException>(() =>
                new RelicDetail(1, RelicStatus.InProgress, null, null, null, ResealAt, 0, FirstPlan));
            Assert.Throws<ArgumentException>(() =>
                new RelicDetail(1, RelicStatus.InProgress, null, null, null, null, 2, FirstPlan));
        }

        /// <summary>완료한 유물은 선명도와 재봉인 시각을 담는지 검사합니다.</summary>
        [Test]
        public void Detail_Completed_WithClarity()
        {
            RelicDetail detail = new RelicDetail(2, RelicStatus.Completed, "정령", Clarity.Faded, LastPurifiedAt, ResealAt, 3, CarePlan);

            Assert.AreEqual(Clarity.Faded, detail.Clarity);
            Assert.AreEqual(ResealAt, detail.ResealAt);
            Assert.AreEqual(3, detail.CareCount);
        }

        /// <summary>이미 재봉인된 유물은 재봉인 시각이 없어도 되는지 검사합니다.</summary>
        [Test]
        public void Detail_CompletedResealed_AllowsNoResealAt()
        {
            RelicDetail detail = new RelicDetail(2, RelicStatus.Completed, "정령", Clarity.Resealed, LastPurifiedAt, null, 1, CarePlan);

            Assert.IsNull(detail.ResealAt);
        }

        /// <summary>정령 이름은 완료한 유물에만 있고, 진행 중이면 null이어야 하는지 검사합니다.</summary>
        [Test]
        public void Detail_SpiritNameOnlyAfterCompleted()
        {
            RelicDetail completed = new RelicDetail(2, RelicStatus.Completed, "깨비", Clarity.Clear, LastPurifiedAt,
                ResealAt, 0, CarePlan);
            Assert.AreEqual("깨비", completed.SpiritName);
            Assert.AreEqual(LastPurifiedAt, completed.LastPurifiedAt);

            // 진행 중에는 이름이 공개되지 않는다. 봉인 해제를 클리어했다면 마지막 정화 시각은 있을 수 있다.
            RelicDetail inProgress = new RelicDetail(1, RelicStatus.InProgress, null, null, LastPurifiedAt, null, 0, FirstPlan);
            Assert.IsNull(inProgress.SpiritName);
            Assert.AreEqual(LastPurifiedAt, inProgress.LastPurifiedAt);

            Assert.Throws<ArgumentException>(() =>
                new RelicDetail(1, RelicStatus.InProgress, "깨비", null, null, null, 0, FirstPlan));
            Assert.Throws<ArgumentException>(() =>
                new RelicDetail(2, RelicStatus.Completed, null, Clarity.Clear, LastPurifiedAt, ResealAt, 0, CarePlan));
            Assert.Throws<ArgumentException>(() =>
                new RelicDetail(2, RelicStatus.Completed, "", Clarity.Clear, LastPurifiedAt, ResealAt, 0, CarePlan));
        }

        /// <summary>완료한 유물에 마지막 정화 시각이 없으면 거부하는지 검사합니다.</summary>
        [Test]
        public void Detail_Completed_RequiresLastPurifiedAt()
        {
            Assert.Throws<ArgumentException>(() =>
                new RelicDetail(2, RelicStatus.Completed, "깨비", Clarity.Clear, null, ResealAt, 0, CarePlan));
        }

        /// <summary>완료한 유물에 선명도가 없거나, 재봉인 전인데 재봉인 시각이 없으면 거부하는지 검사합니다.</summary>
        [Test]
        public void Detail_Completed_MissingFields_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new RelicDetail(2, RelicStatus.Completed, "정령", null, LastPurifiedAt, ResealAt, 0, CarePlan));
            Assert.Throws<ArgumentException>(() =>
                new RelicDetail(2, RelicStatus.Completed, "정령", Clarity.Clear, LastPurifiedAt, null, 0, CarePlan));
        }

        /// <summary>모양이 잘못된 값을 거부하는지 검사합니다.</summary>
        [Test]
        public void Detail_InvalidValues_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RelicDetail(0, RelicStatus.InProgress, null, null, null, null, 0, FirstPlan));
            Assert.Throws<ArgumentException>(() =>
                new RelicDetail(1, (RelicStatus)99, null, null, null, null, 0, FirstPlan));
            Assert.Throws<ArgumentException>(() =>
                new RelicDetail(1, RelicStatus.Completed, "정령", (Clarity)99, LastPurifiedAt, ResealAt, 0, CarePlan));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RelicDetail(1, RelicStatus.Completed, "정령", Clarity.Clear, LastPurifiedAt, ResealAt, -1, CarePlan));
            Assert.Throws<ArgumentNullException>(() =>
                new RelicDetail(1, RelicStatus.InProgress, null, null, null, null, 0, null));
        }

        // ---------- BoxOpenOutcome ----------

        /// <summary>무료로 새 유물을 얻으면 모든 재화가 0인 결과를 만들 수 있는지 검사합니다.</summary>
        [Test]
        public void BoxOutcome_NewFree_AllZero()
        {
            BoxOpenOutcome outcome = new BoxOpenOutcome(AcquireResult.New, 3, 0, 0, 0);

            Assert.AreEqual(AcquireResult.New, outcome.Result);
            Assert.AreEqual(3, outcome.RelicId);
            Assert.AreEqual(0, outcome.JeongseongGained);
            Assert.AreEqual(0, outcome.CrystalGained);
            Assert.AreEqual(0, outcome.JeongseongSpent);
        }

        /// <summary>구매로 새 유물을 얻으면 쓴 정성만 있는 결과를 만들 수 있는지 검사합니다.</summary>
        [Test]
        public void BoxOutcome_NewPurchase_OnlySpent()
        {
            BoxOpenOutcome outcome = new BoxOpenOutcome(AcquireResult.New, 3, 0, 0, 50);

            Assert.AreEqual(50, outcome.JeongseongSpent);
        }

        /// <summary>중복이면 정성·결정을 받은 결과를 만들 수 있는지 검사합니다.</summary>
        [Test]
        public void BoxOutcome_Duplicate_StoresGains()
        {
            BoxOpenOutcome outcome = new BoxOpenOutcome(AcquireResult.Duplicate, 3, 20, 1, 0);

            Assert.AreEqual(AcquireResult.Duplicate, outcome.Result);
            Assert.AreEqual(20, outcome.JeongseongGained);
            Assert.AreEqual(1, outcome.CrystalGained);
        }

        /// <summary>새 유물인데 환전 재화가 있으면 거부하는지 검사합니다.</summary>
        [Test]
        public void BoxOutcome_NewWithGains_Throws()
        {
            Assert.Throws<ArgumentException>(() => new BoxOpenOutcome(AcquireResult.New, 3, 20, 0, 0));
            Assert.Throws<ArgumentException>(() => new BoxOpenOutcome(AcquireResult.New, 3, 0, 1, 0));
        }

        /// <summary>모양이 잘못된 값(정의되지 않은 결과, relicId 1 미만, 음수 재화)을 거부하는지 검사합니다.</summary>
        [Test]
        public void BoxOutcome_InvalidValues_Throw()
        {
            Assert.Throws<ArgumentException>(() => new BoxOpenOutcome((AcquireResult)99, 3, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoxOpenOutcome(AcquireResult.New, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoxOpenOutcome(AcquireResult.Duplicate, 3, -1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoxOpenOutcome(AcquireResult.Duplicate, 3, 0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoxOpenOutcome(AcquireResult.New, 3, 0, 0, -1));
        }

        // ---------- PurificationSession ----------

        /// <summary>올바른 값이면 그대로 담는지 검사합니다.</summary>
        [Test]
        public void Session_StoresValues()
        {
            PurificationSession session = new PurificationSession("token-1", 4, EntryRoute.Care,
                Phase.Recall, MinigameDifficulty.Hard, SealMedium.Soot, -42L);

            Assert.AreEqual("token-1", session.SessionToken);
            Assert.AreEqual(4, session.RelicId);
            Assert.AreEqual(EntryRoute.Care, session.Route);
            Assert.AreEqual(Phase.Recall, session.Phase);
            Assert.AreEqual(MinigameDifficulty.Hard, session.Difficulty);
            Assert.AreEqual(SealMedium.Soot, session.SealMedium);
            Assert.AreEqual(-42L, session.Seed);

            PlaySetup setup = session.ToSetup();
            Assert.AreEqual(Phase.Recall, setup.Phase);
            Assert.AreEqual(MinigameDifficulty.Hard, setup.Difficulty);
            Assert.AreEqual(SealMedium.Soot, setup.SealMedium);
            Assert.AreEqual(-42L, setup.Seed);
        }

        /// <summary>정의되지 않은 봉인 매개체를 거부하는지 검사합니다.</summary>
        [Test]
        public void Session_UndefinedSealMedium_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new PurificationSession("t", 4, EntryRoute.First, Phase.Unseal, MinigameDifficulty.Normal, (SealMedium)99, 1));
        }

        /// <summary>토큰이 null이거나 비어 있으면 거부하는지 검사합니다.</summary>
        [Test]
        public void Session_MissingToken_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PurificationSession(null, 4, EntryRoute.First, Phase.Unseal, MinigameDifficulty.Normal, SealMedium.Rust, 7));
            Assert.Throws<ArgumentException>(() =>
                new PurificationSession("", 4, EntryRoute.First, Phase.Unseal, MinigameDifficulty.Normal, SealMedium.Rust, 7));
        }

        /// <summary>
        /// 모양이 잘못된 값을 거부하는지 검사합니다.
        /// MinigameDifficulty는 1부터 시작하므로 기본값 0도 정의되지 않은 값입니다.
        /// </summary>
        [Test]
        public void Session_InvalidValues_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PurificationSession("t", 0, EntryRoute.First, Phase.Unseal, MinigameDifficulty.Normal, SealMedium.Rust, 7));
            Assert.Throws<ArgumentException>(() =>
                new PurificationSession("t", 4, (EntryRoute)99, Phase.Unseal, MinigameDifficulty.Normal, SealMedium.Rust, 7));
            Assert.Throws<ArgumentException>(() =>
                new PurificationSession("t", 4, EntryRoute.First, (Phase)99, MinigameDifficulty.Normal, SealMedium.Rust, 7));
            Assert.Throws<ArgumentException>(() =>
                new PurificationSession("t", 4, EntryRoute.First, Phase.Unseal, default(MinigameDifficulty), SealMedium.Rust, 7));
        }

        // ---------- IWorkbenchBackend ----------

        /// <summary>
        /// 모든 메서드가 코루틴(IEnumerator)을 돌려주고, 끝의 두 매개변수가
        /// 성공 콜백 Action&lt;T&gt;와 실패 콜백 Action&lt;string&gt;인지 검사합니다.
        /// </summary>
        [Test]
        public void Backend_AllMethodsAreCoroutinesWithCallbacks()
        {
            MethodInfo[] methods = typeof(IWorkbenchBackend).GetMethods();
            Assert.AreEqual(6, methods.Length);

            foreach (MethodInfo method in methods)
            {
                ParameterInfo[] parameters = method.GetParameters();
                Assert.AreEqual(typeof(IEnumerator), method.ReturnType, method.Name);
                Assert.GreaterOrEqual(parameters.Length, 2, method.Name);

                Type completed = parameters[parameters.Length - 2].ParameterType;
                Assert.IsTrue(completed.IsGenericType && completed.GetGenericTypeDefinition() == typeof(Action<>),
                    method.Name + "의 성공 콜백");
                Assert.AreEqual(typeof(Action<string>), parameters[parameters.Length - 1].ParameterType,
                    method.Name + "의 실패 콜백");
            }
        }

        /// <summary>메서드마다 서버 API에 맞는 입력과 성공 결과 타입을 쓰는지 검사합니다.</summary>
        [TestCase("GetWorkbench", typeof(WorkbenchSnapshot))]
        [TestCase("GetRelicDetail", typeof(RelicDetail), typeof(int))]
        [TestCase("OpenBox", typeof(BoxOpenOutcome), typeof(BoxSource), typeof(string))]
        [TestCase("StartPurification", typeof(PurificationSession), typeof(int), typeof(MinigameDifficulty?))]
        [TestCase("ReportPurification", typeof(SettlementResult), typeof(string), typeof(PlayReport))]
        [TestCase("RenameSpirit", typeof(string), typeof(int), typeof(string))]
        public void Backend_MethodSignature(string name, Type result, params Type[] inputs)
        {
            MethodInfo method = typeof(IWorkbenchBackend).GetMethod(name);
            Assert.IsNotNull(method, name);

            Type[] parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
            CollectionAssert.AreEqual(inputs, parameters.Take(parameters.Length - 2), name + "의 입력");
            Assert.AreEqual(typeof(Action<>).MakeGenericType(result), parameters[parameters.Length - 2], name + "의 결과");
        }
    }
}
