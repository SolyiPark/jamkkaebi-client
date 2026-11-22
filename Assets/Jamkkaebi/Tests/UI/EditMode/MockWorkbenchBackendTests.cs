using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;
using Jamkkaebi.Scripts.UI.Workbench;
using NUnit.Framework;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>
    /// 모의 작업대 백엔드가 서버 규칙(상자 멱등, 가득 참, 난이도 결정, 판 대체, 재보고·충돌, 시작 시점 계획)을 지키는지 검사합니다.
    /// 지연을 0으로 두고 코루틴을 직접 끝까지 돌립니다.
    /// </summary>
    public sealed class MockWorkbenchBackendTests
    {
        private static readonly DateTime BaseTime = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);

        private DateTime _now;
        private RelicInventory _inventory;
        private QueuedRandom _random;
        private MockWorkbenchBackend _backend;

        [SetUp]
        public void SetUp()
        {
            _now = BaseTime + TimeSpan.FromDays(30);
        }

        // ---------- 준비 도우미 ----------

        /// <summary>
        /// 테스트용 유물 카탈로그. relicId 1 이상이면 기본 정령 이름 "정령{id}"와 봉인 매개체를 돌려준다.
        /// unknownIds에 든 relicId는 모르는 유물로 본다.
        /// </summary>
        private sealed class TestCatalog : IWorkbenchRelicCatalog
        {
            private readonly int[] _unknownIds;
            public TestCatalog(int count, params int[] unknownIds) { Count = count; _unknownIds = unknownIds; }
            public int Count { get; }

            public bool TryFind(int relicId, out WorkbenchRelicInfo info)
            {
                if (relicId < 1 || Array.IndexOf(_unknownIds, relicId) >= 0)
                {
                    info = null;
                    return false;
                }
                info = new WorkbenchRelicInfo(relicId, "시대", "별칭" + relicId, "유물" + relicId, "정령" + relicId,
                    relicId % 2 == 0 ? SealMedium.Fog : SealMedium.Root);
                return true;
            }
        }

        private static readonly TestCatalog SpiritName = new TestCatalog(10);

        private static PlayReport ReportOf(PlayResult result) => result == PlayResult.Success
            ? new PlayReport(PlayResult.Success, null, 3, 1, 10, 0)
            : new PlayReport(PlayResult.Failure, FailReason.DamageFull, 3, 1, 100, 0);

        private static PlayPlanner CreatePlanner() =>
            new PlayPlanner(ClarityPolicy.Default, RestorePolicy.Default, MinigameDifficulty.Normal);

        private void CreateBackend(int capacity, int totalRelics, params OwnedRelic[] relics)
        {
            _inventory = new RelicInventory(ClarityPolicy.Default, capacity, relics);
            _random = new QueuedRandom();
            _backend = new MockWorkbenchBackend(_inventory, CreatePlanner(), totalRelics, SpiritName, _random, () => _now, 0f);
        }

        /// <summary>최초 정화 중인 유물. BaseTime에서 hoursLater시간 뒤에 획득했다.</summary>
        private static OwnedRelic InProgress(int relicId, double hoursLater = 0) =>
            OwnedRelic.Acquired(relicId, BaseTime + TimeSpan.FromHours(hoursLater));

        /// <summary>최초 정화를 마친 정령. 마지막 정화 시각을 직접 정한다.</summary>
        private static OwnedRelic Completed(int relicId, DateTime lastPurifiedAt) =>
            new OwnedRelic(relicId, BaseTime, RelicStatus.Completed, Phase.Guard, lastPurifiedAt, null, null, 0);

        private OwnedRelic CompletedDaysAgo(int relicId, double daysAgo) =>
            Completed(relicId, _now - TimeSpan.FromDays(daysAgo));

        // ---------- 코루틴 실행 도우미 ----------

        private sealed class CallResult<T>
        {
            public T Value;
            public string Error;
            public int CompletedCount;
            public int FailedCount;
            public bool Succeeded => CompletedCount == 1;
        }

        /// <summary>코루틴을 끝까지 돌리고, 콜백이 정확히 한 번만 불렸는지 확인한다.</summary>
        private static CallResult<T> Run<T>(Func<Action<T>, Action<string>, IEnumerator> call)
        {
            CallResult<T> result = new CallResult<T>();
            Drain(call(value => { result.Value = value; result.CompletedCount++; },
                error => { result.Error = error; result.FailedCount++; }));
            Assert.AreEqual(1, result.CompletedCount + result.FailedCount, "completed와 failed 중 정확히 하나만 한 번 불려야 합니다.");
            return result;
        }

        /// <summary>안쪽 코루틴(yield return Delay())까지 따라 들어가며 끝까지 돌린다.</summary>
        private static void Drain(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator inner) Drain(inner);
            }
        }

        private CallResult<WorkbenchSnapshot> Workbench() =>
            Run<WorkbenchSnapshot>((c, f) => _backend.GetWorkbench(c, f));

        private CallResult<RelicDetail> Detail(int relicId) =>
            Run<RelicDetail>((c, f) => _backend.GetRelicDetail(relicId, c, f));

        private CallResult<BoxOpenOutcome> Open(BoxSource source, string requestId) =>
            Run<BoxOpenOutcome>((c, f) => _backend.OpenBox(source, requestId, c, f));

        private CallResult<PurificationSession> Start(int relicId, MinigameDifficulty? difficulty) =>
            Run<PurificationSession>((c, f) => _backend.StartPurification(relicId, difficulty, c, f));

        private CallResult<SettlementResult> Report(string token, PlayResult result) =>
            Report(token, ReportOf(result));

        private CallResult<SettlementResult> Report(string token, PlayReport report) =>
            Run<SettlementResult>((c, f) => _backend.ReportPurification(token, report, c, f));

        /// <summary>뽑을 relicId를 미리 정해 두는 Random. 예상하지 못한 뽑기가 일어나면 바로 실패한다.</summary>
        private sealed class QueuedRandom : Random
        {
            private readonly Queue<int> _values = new Queue<int>();
            public int MinRequested { get; private set; }
            public int MaxRequested { get; private set; }
            public int Calls { get; private set; }

            public void Enqueue(params int[] values)
            {
                foreach (int value in values) _values.Enqueue(value);
            }

            public override int Next(int minValue, int maxValue)
            {
                Calls++;
                MinRequested = minValue;
                MaxRequested = maxValue;
                if (_values.Count == 0) throw new InvalidOperationException("예상하지 못한 뽑기입니다.");
                return _values.Dequeue();
            }
        }

        // ---------- 생성자 ----------

        /// <summary>null 인자를 ArgumentNullException으로 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_NullArguments_Throw()
        {
            RelicInventory inventory = new RelicInventory(ClarityPolicy.Default, 3, new OwnedRelic[0]);
            PlayPlanner planner = CreatePlanner();
            Random random = new Random(1);
            Func<DateTime> clock = () => _now;

            Assert.Throws<ArgumentNullException>(() => new MockWorkbenchBackend(null, planner, 10, SpiritName, random, clock, 0f));
            Assert.Throws<ArgumentNullException>(() => new MockWorkbenchBackend(inventory, null, 10, SpiritName, random, clock, 0f));
            Assert.Throws<ArgumentNullException>(() => new MockWorkbenchBackend(inventory, planner, 10, SpiritName, null, clock, 0f));
            Assert.Throws<ArgumentNullException>(() => new MockWorkbenchBackend(inventory, planner, 10, SpiritName, random, null, 0f));
        }

        /// <summary>전체 유물 수 1 미만, 가진 유물이 전체보다 많은 경우, 음수 지연을 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_InvalidNumbers_Throw()
        {
            RelicInventory three = new RelicInventory(ClarityPolicy.Default, 3, new[] { InProgress(1), InProgress(2), InProgress(3) });
            PlayPlanner planner = CreatePlanner();
            Random random = new Random(1);
            Func<DateTime> clock = () => _now;

            Assert.Throws<ArgumentOutOfRangeException>(() => new MockWorkbenchBackend(three, planner, 0, SpiritName, random, clock, 0f));
            Assert.Throws<ArgumentException>(() => new MockWorkbenchBackend(three, planner, 2, SpiritName, random, clock, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MockWorkbenchBackend(three, planner, 10, SpiritName, random, clock, -1f));
        }

        /// <summary>
        /// 개수는 맞아도 뽑기 범위 밖 relicId가 있으면 거부하는지 검사합니다.
        /// (받아들이면 범위 안 유물을 뽑은 뒤 lockedCount가 음수가 된다)
        /// </summary>
        [Test]
        public void Constructor_RelicIdOutOfRange_Throws()
        {
            RelicInventory inventory = new RelicInventory(ClarityPolicy.Default, 3, new[] { CompletedDaysAgo(2, 1) });

            Assert.Throws<ArgumentException>(() =>
                new MockWorkbenchBackend(inventory, CreatePlanner(), 1, SpiritName, new Random(1), () => _now, 0f));
            Assert.DoesNotThrow(() =>
                new MockWorkbenchBackend(inventory, CreatePlanner(), 2, SpiritName, new Random(1), () => _now, 0f));
        }

        /// <summary>인벤토리와 계획기의 선명도 정책이 다르면 거부하는지 검사합니다(정렬과 표시가 어긋나지 않게).</summary>
        [Test]
        public void Constructor_DifferentClarityPolicies_Throws()
        {
            ClarityPolicy other = new ClarityPolicy(TimeSpan.FromDays(3), TimeSpan.FromDays(5));
            RelicInventory inventory = new RelicInventory(other, 3, new OwnedRelic[0]);

            Assert.Throws<ArgumentException>(() =>
                new MockWorkbenchBackend(inventory, CreatePlanner(), 10, SpiritName, new Random(1), () => _now, 0f));

            PlayPlanner matching = new PlayPlanner(other, RestorePolicy.Default, MinigameDifficulty.Normal);
            Assert.DoesNotThrow(() =>
                new MockWorkbenchBackend(inventory, matching, 10, SpiritName, new Random(1), () => _now, 0f));
        }

        // ---------- GetWorkbench ----------

        /// <summary>
        /// 인벤토리를 작업대 응답으로 바꾸는지 검사합니다.
        /// 슬롯은 획득 순, 정령은 재봉인 → 흐려짐 → 선명 순이고 선명도를 계산해 담습니다.
        /// </summary>
        [Test]
        public void GetWorkbench_ConvertsInventory()
        {
            CreateBackend(3, 10,
                InProgress(7, 2), InProgress(5, 0),
                CompletedDaysAgo(4, 1), CompletedDaysAgo(2, 20), CompletedDaysAgo(3, 10));

            WorkbenchSnapshot snapshot = Workbench().Value;

            Assert.AreEqual(3, snapshot.Capacity);
            Assert.AreEqual(5, snapshot.LockedCount);
            CollectionAssert.AreEqual(new[] { 5, 7 }, snapshot.Slots.Select(s => s.RelicId));
            Assert.AreEqual(Phase.Unseal, snapshot.Slots[0].CurrentPhase);
            Assert.AreEqual(RevealLevel.Silhouette, snapshot.Slots[0].RevealLevel);
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, snapshot.Spirits.Select(s => s.RelicId));
            CollectionAssert.AreEqual(new[] { Clarity.Resealed, Clarity.Faded, Clarity.Clear },
                snapshot.Spirits.Select(s => s.Clarity));
            Assert.IsFalse(snapshot.IsFull);
        }

        /// <summary>시계가 움직이면 같은 정령의 선명도가 바뀌는지 검사합니다.</summary>
        [Test]
        public void GetWorkbench_UsesClock()
        {
            CreateBackend(3, 10, CompletedDaysAgo(1, 1));
            Assert.AreEqual(Clarity.Clear, Workbench().Value.Spirits[0].Clarity);

            _now += TimeSpan.FromDays(7);

            Assert.AreEqual(Clarity.Faded, Workbench().Value.Spirits[0].Clarity);
        }

        // ---------- GetRelicDetail ----------

        /// <summary>가진 유물이 아니면 failed만 부르는지 검사합니다.</summary>
        [Test]
        public void GetRelicDetail_NotOwned_Fails()
        {
            CreateBackend(3, 10, InProgress(1));

            CallResult<RelicDetail> result = Detail(9);

            Assert.AreEqual(1, result.FailedCount);
            Assert.IsNull(result.Value);
        }

        /// <summary>최초 정화 중이면 선명도·재봉인 시각 없이 최초 정화 계획을 담는지 검사합니다.</summary>
        [Test]
        public void GetRelicDetail_InProgress_NoClarity()
        {
            CreateBackend(3, 10, InProgress(1));

            RelicDetail detail = Detail(1).Value;

            Assert.AreEqual(RelicStatus.InProgress, detail.Status);
            Assert.IsNull(detail.Clarity);
            Assert.IsNull(detail.ResealAt);
            Assert.AreEqual(EntryRoute.First, detail.NextPlay.Route);
            Assert.AreEqual(Phase.Unseal, detail.NextPlay.Phase);
        }

        /// <summary>완료한 정령이면 선명도와 재봉인 시각(마지막 정화 + 14일), 관리 계획을 담는지 검사합니다.</summary>
        [Test]
        public void GetRelicDetail_Completed_HasClarityAndResealAt()
        {
            DateTime lastPurifiedAt = _now - TimeSpan.FromDays(10);
            CreateBackend(3, 10, Completed(2, lastPurifiedAt));

            RelicDetail detail = Detail(2).Value;

            Assert.AreEqual(Clarity.Faded, detail.Clarity);
            Assert.AreEqual(lastPurifiedAt + ClarityPolicy.DefaultResealedAfter, detail.ResealAt);
            Assert.AreEqual(EntryRoute.Care, detail.NextPlay.Route);
            Assert.AreEqual(Clarity.Faded, detail.NextPlay.Clarity);
        }

        // ---------- OpenBox ----------

        /// <summary>1부터 전체 유물 수까지(끝 포함)에서 뽑는지 검사합니다. Random.Next의 max는 포함되지 않습니다.</summary>
        [Test]
        public void OpenBox_DrawsFromOneToTotalInclusive()
        {
            CreateBackend(3, 10);
            _random.Enqueue(10);

            Open(BoxSource.Free, "a");

            Assert.AreEqual(1, _random.MinRequested);
            Assert.AreEqual(11, _random.MaxRequested);
        }

        /// <summary>무료로 새 유물을 뽑으면 재화 없이 진행 중 유물로 추가되는지 검사합니다.</summary>
        [Test]
        public void OpenBox_NewFree_AddsRelic()
        {
            CreateBackend(3, 10);
            _random.Enqueue(4);

            BoxOpenOutcome outcome = Open(BoxSource.Free, "a").Value;

            Assert.AreEqual(AcquireResult.New, outcome.Result);
            Assert.AreEqual(4, outcome.RelicId);
            Assert.AreEqual(0, outcome.JeongseongGained);
            Assert.AreEqual(0, outcome.CrystalGained);
            Assert.AreEqual(0, outcome.JeongseongSpent);
            Assert.IsTrue(_inventory.TryGet(4, out OwnedRelic relic));
            Assert.AreEqual(RelicStatus.InProgress, relic.Status);
            Assert.AreEqual(_now, relic.AcquiredAt);
        }

        /// <summary>구매면 정성을 쓰고, 선물이면 쓰지 않는지 검사합니다.</summary>
        [Test]
        public void OpenBox_PurchaseSpendsJeongseong()
        {
            CreateBackend(3, 10);
            _random.Enqueue(1, 2);

            Assert.AreEqual(MockWorkbenchBackend.PurchasePrice, Open(BoxSource.Purchase, "a").Value.JeongseongSpent);
            Assert.AreEqual(0, Open(BoxSource.Gift, "b").Value.JeongseongSpent);
        }

        /// <summary>이미 가진 유물이면 중복으로 정성·결정을 주고 인벤토리는 그대로인지 검사합니다.</summary>
        [Test]
        public void OpenBox_Duplicate_GivesCurrency()
        {
            OwnedRelic owned = CompletedDaysAgo(2, 1);
            CreateBackend(3, 10, owned);
            _random.Enqueue(2);

            BoxOpenOutcome outcome = Open(BoxSource.Free, "a").Value;

            Assert.AreEqual(AcquireResult.Duplicate, outcome.Result);
            Assert.AreEqual(MockWorkbenchBackend.DuplicateJeongseong, outcome.JeongseongGained);
            Assert.AreEqual(MockWorkbenchBackend.DuplicateCrystal, outcome.CrystalGained);
            Assert.AreEqual(1, _inventory.Count);
            Assert.AreEqual(RelicStatus.Completed, owned.Status);
        }

        /// <summary>같은 requestId로 다시 보내면 다시 뽑지 않고 처음 결과를 그대로 돌려주는지 검사합니다.</summary>
        [Test]
        public void OpenBox_SameRequestId_ReturnsSameOutcome()
        {
            CreateBackend(3, 10);
            _random.Enqueue(4);

            BoxOpenOutcome first = Open(BoxSource.Free, "a").Value;
            BoxOpenOutcome retry = Open(BoxSource.Free, "a").Value;

            Assert.AreSame(first, retry);
            Assert.AreEqual(1, _random.Calls);
            Assert.AreEqual(1, _inventory.Count);
        }

        /// <summary>
        /// 마지막 칸을 채운 응답을 놓쳐 같은 requestId로 다시 보내면, 이제 가득 찼어도 처음 결과를 받는지 검사합니다.
        /// (가득 참 검사보다 기억한 결과 확인이 먼저)
        /// </summary>
        [Test]
        public void OpenBox_RetryAfterFillingLastSlot_ReturnsSameOutcome()
        {
            CreateBackend(1, 10);
            _random.Enqueue(4);

            BoxOpenOutcome first = Open(BoxSource.Free, "a").Value;
            Assert.IsTrue(_inventory.IsWorkbenchFull);

            CallResult<BoxOpenOutcome> retry = Open(BoxSource.Free, "a");

            Assert.IsTrue(retry.Succeeded);
            Assert.AreSame(first, retry.Value);
        }

        /// <summary>작업대가 가득 차면 뽑지 않고 실패하는지 검사합니다.</summary>
        [Test]
        public void OpenBox_WhenFull_FailsWithoutDrawing()
        {
            CreateBackend(1, 10, InProgress(1));

            CallResult<BoxOpenOutcome> result = Open(BoxSource.Free, "a");

            Assert.AreEqual(1, result.FailedCount);
            Assert.AreEqual(0, _random.Calls);
            Assert.AreEqual(1, _inventory.Count);
        }

        /// <summary>
        /// 가득 차서 실패한 requestId는 기억하지 않아, 칸이 빈 뒤 같은 번호로 보내면 새로 뽑는지 검사합니다.
        /// </summary>
        [Test]
        public void OpenBox_FailureIsNotRemembered()
        {
            OwnedRelic relic = InProgress(1);
            CreateBackend(1, 10, relic);
            Assert.AreEqual(1, Open(BoxSource.Free, "a").FailedCount);

            relic.ClearFirstPlay(_now);
            relic.ClearFirstPlay(_now);
            relic.ClearFirstPlay(_now);
            _random.Enqueue(5);

            CallResult<BoxOpenOutcome> retry = Open(BoxSource.Free, "a");

            Assert.IsTrue(retry.Succeeded);
            Assert.AreEqual(5, retry.Value.RelicId);
        }

        /// <summary>
        /// completed 안에서 같은 requestId로 다시 불러도 같은 결과를 받는지 검사합니다.
        /// (결과를 기억한 뒤 completed를 부르는지)
        /// </summary>
        [Test]
        public void OpenBox_RemembersBeforeCallingCompleted()
        {
            CreateBackend(3, 10);
            _random.Enqueue(4);
            BoxOpenOutcome inner = null;

            CallResult<BoxOpenOutcome> outer = Run<BoxOpenOutcome>((c, f) => _backend.OpenBox(BoxSource.Free, "a",
                outcome =>
                {
                    c(outcome);
                    inner = Open(BoxSource.Free, "a").Value;
                }, f));

            Assert.AreSame(outer.Value, inner);
            Assert.AreEqual(1, _random.Calls);
        }

        /// <summary>정의되지 않은 경로나 비어 있는 requestId는 뽑지 않고 실패하는지 검사합니다.</summary>
        [Test]
        public void OpenBox_InvalidInput_FailsWithoutDrawing()
        {
            CreateBackend(3, 10);

            Assert.AreEqual(1, Open((BoxSource)99, "a").FailedCount);
            Assert.AreEqual(1, Open(BoxSource.Free, null).FailedCount);
            Assert.AreEqual(1, Open(BoxSource.Free, "").FailedCount);
            Assert.AreEqual(0, _random.Calls);
        }

        // ---------- StartPurification ----------

        /// <summary>가진 유물이 아니면 실패하는지 검사합니다.</summary>
        [Test]
        public void Start_NotOwned_Fails()
        {
            CreateBackend(3, 10);

            Assert.AreEqual(1, Start(1, null).FailedCount);
        }

        /// <summary>최초 정화에서 난이도를 비우면 고정 난이도(보통)로 봉인 해제부터 시작하는지 검사합니다.</summary>
        [Test]
        public void Start_FirstRoute_NullUsesFixedDifficulty()
        {
            CreateBackend(3, 10, InProgress(1));

            PurificationSession session = Start(1, null).Value;

            Assert.AreEqual(1, session.RelicId);
            Assert.AreEqual(EntryRoute.First, session.Route);
            Assert.AreEqual(Phase.Unseal, session.Phase);
            Assert.AreEqual(MinigameDifficulty.Normal, session.Difficulty);
            Assert.IsFalse(string.IsNullOrEmpty(session.SessionToken));
        }

        /// <summary>최초 정화에 허용되지 않은 난이도를 보내면 무시하지 않고 실패하는지 검사합니다(서버 resolveDifficulty와 같음).</summary>
        [Test]
        public void Start_FirstRoute_DisallowedDifficulty_Fails()
        {
            CreateBackend(3, 10, InProgress(1));

            Assert.AreEqual(1, Start(1, MinigameDifficulty.Hard).FailedCount);
        }

        /// <summary>선명할 때는 난이도를 골라야 하고, 쉬움도 고를 수 있는지 검사합니다.</summary>
        [Test]
        public void Start_CareClear_RequiresChoiceAndAllowsEasy()
        {
            CreateBackend(3, 10, CompletedDaysAgo(2, 1));

            Assert.AreEqual(1, Start(2, null).FailedCount);

            PurificationSession session = Start(2, MinigameDifficulty.Easy).Value;
            Assert.AreEqual(EntryRoute.Care, session.Route);
            Assert.AreEqual(MinigameDifficulty.Easy, session.Difficulty);
        }

        /// <summary>흐려졌을 때는 복구가 없는 쉬움을 고를 수 없는지 검사합니다.</summary>
        [Test]
        public void Start_CareFaded_RejectsEasy()
        {
            CreateBackend(3, 10, CompletedDaysAgo(2, 10));

            Assert.AreEqual(1, Start(2, MinigameDifficulty.Easy).FailedCount);
            Assert.AreEqual(Phase.Recall, Start(2, MinigameDifficulty.Normal).Value.Phase);
        }

        /// <summary>정의되지 않은 난이도 값은 계획의 허용 목록에서 걸러져 실패하는지 검사합니다.</summary>
        [Test]
        public void Start_UndefinedDifficulty_Fails()
        {
            CreateBackend(3, 10, CompletedDaysAgo(2, 1));

            Assert.AreEqual(1, Start(2, (MinigameDifficulty)99).FailedCount);
        }

        /// <summary>같은 유물로 새 판을 시작하면 보고하지 않은 이전 판은 더 보고할 수 없는지 검사합니다.</summary>
        [Test]
        public void Start_SameRelic_SupersedesPreviousPlay()
        {
            CreateBackend(3, 10, InProgress(1));
            string oldToken = Start(1, null).Value.SessionToken;
            string newToken = Start(1, null).Value.SessionToken;

            Assert.AreNotEqual(oldToken, newToken);
            Assert.AreEqual(1, Report(oldToken, PlayResult.Success).FailedCount);
            Assert.IsTrue(Report(newToken, PlayResult.Success).Succeeded);
        }

        /// <summary>잘못된 난이도로 다시 시작해도 진행 중인 이전 판은 그대로 남는지 검사합니다(검사 → 대체 순서).</summary>
        [Test]
        public void Start_InvalidRetry_KeepsPreviousPlay()
        {
            CreateBackend(3, 10, CompletedDaysAgo(2, 10));
            string token = Start(2, MinigameDifficulty.Normal).Value.SessionToken;

            Assert.AreEqual(1, Start(2, MinigameDifficulty.Easy).FailedCount);

            Assert.IsTrue(Report(token, PlayResult.Success).Succeeded);
        }

        /// <summary>다른 유물의 판은 서로 대체하지 않는지 검사합니다.</summary>
        [Test]
        public void Start_DifferentRelics_DoNotSupersede()
        {
            CreateBackend(3, 10, InProgress(1), InProgress(2));
            string first = Start(1, null).Value.SessionToken;
            string second = Start(2, null).Value.SessionToken;

            Assert.IsTrue(Report(first, PlayResult.Success).Succeeded);
            Assert.IsTrue(Report(second, PlayResult.Success).Succeeded);
        }

        // ---------- ReportPurification ----------

        /// <summary>최초 정화 성공을 정산하면 다음 페이즈로 넘어가는지 검사합니다.</summary>
        [Test]
        public void Report_FirstSuccess_AdvancesPhase()
        {
            OwnedRelic relic = InProgress(1);
            CreateBackend(3, 10, relic);
            string token = Start(1, null).Value.SessionToken;

            SettlementResult result = Report(token, PlayResult.Success).Value;

            Assert.AreEqual(PlayResult.Success, result.Result);
            Assert.AreEqual(NextAction.NextPhase, result.Next);
            Assert.AreEqual(Phase.Recall, relic.CurrentPhase);
        }

        /// <summary>실패를 정산하면 같은 페이즈에 머무는지 검사합니다.</summary>
        [Test]
        public void Report_Failure_StaysOnPhase()
        {
            OwnedRelic relic = InProgress(1);
            CreateBackend(3, 10, relic);
            string token = Start(1, null).Value.SessionToken;

            Assert.AreEqual(NextAction.Retry, Report(token, PlayResult.Failure).Value.Next);
            Assert.AreEqual(Phase.Unseal, relic.CurrentPhase);
        }

        /// <summary>같은 토큰·같은 결과로 다시 보고하면 다시 정산하지 않고 처음 결과를 그대로 돌려주는지 검사합니다.</summary>
        [Test]
        public void Report_SameResultTwice_ReturnsSameSettlement()
        {
            OwnedRelic relic = InProgress(1);
            CreateBackend(3, 10, relic);
            string token = Start(1, null).Value.SessionToken;

            SettlementResult first = Report(token, PlayResult.Success).Value;
            CallResult<SettlementResult> retry = Report(token, PlayResult.Success);

            Assert.IsTrue(retry.Succeeded);
            Assert.AreSame(first, retry.Value);
            Assert.AreEqual(Phase.Recall, relic.CurrentPhase);
        }

        /// <summary>이미 정산한 토큰에 다른 결과를 보내면 충돌로 실패하고 상태가 그대로인지 검사합니다.</summary>
        [Test]
        public void Report_DifferentResultAfterSettled_Fails()
        {
            OwnedRelic relic = InProgress(1);
            CreateBackend(3, 10, relic);
            string token = Start(1, null).Value.SessionToken;
            Report(token, PlayResult.Failure);

            CallResult<SettlementResult> changed = Report(token, PlayResult.Success);

            Assert.AreEqual(1, changed.FailedCount);
            Assert.AreEqual(Phase.Unseal, relic.CurrentPhase);
        }

        /// <summary>발급한 적 없는 토큰이나 비어 있는 토큰은 실패하는지 검사합니다.</summary>
        [Test]
        public void Report_UnknownOrEmptyToken_Fails()
        {
            CreateBackend(3, 10, InProgress(1));

            Assert.AreEqual(1, Report("unknown", PlayResult.Success).FailedCount);
            Assert.AreEqual(1, Report(null, PlayResult.Success).FailedCount);
            Assert.AreEqual(1, Report("", PlayResult.Success).FailedCount);
        }

        /// <summary>보고가 null이면 판을 소모하지 않고 실패해, 나중에 올바른 결과로 보고할 수 있는지 검사합니다.</summary>
        [Test]
        public void Report_NullReport_FailsAndKeepsPlay()
        {
            CreateBackend(3, 10, InProgress(1));
            string token = Start(1, null).Value.SessionToken;

            Assert.AreEqual(1, Report(token, (PlayReport)null).FailedCount);

            Assert.IsTrue(Report(token, PlayResult.Success).Succeeded);
        }

        /// <summary>
        /// 흐려짐에서 기억 회복을 시작하고, 재봉인 경계를 넘긴 뒤 보고하면 시작 시점 계획으로 정산하는지 검사합니다.
        /// (지금 상태로 계획을 새로 만들면 봉인 해제부터가 되어 진행이 바뀐다)
        /// </summary>
        [Test]
        public void Report_UsesPlanFromStart_AcrossResealedBoundary()
        {
            DateTime lastPurifiedAt = BaseTime;
            OwnedRelic relic = Completed(2, lastPurifiedAt);
            _now = lastPurifiedAt + ClarityPolicy.DefaultResealedAfter - TimeSpan.FromMinutes(1);
            CreateBackend(3, 10, relic);

            PurificationSession session = Start(2, MinigameDifficulty.Normal).Value;
            Assert.AreEqual(Phase.Recall, session.Phase);

            _now = lastPurifiedAt + ClarityPolicy.DefaultResealedAfter + TimeSpan.FromMinutes(1);
            SettlementResult result = Report(session.SessionToken, PlayResult.Success).Value;

            Assert.AreEqual(Phase.Recall, relic.RestorePhase);
            Assert.AreEqual(Clarity.Resealed, relic.RestoreClarity);
            Assert.AreEqual(NextAction.ContinueRestore, result.Next);
            CollectionAssert.AreEqual(new[] { Phase.Guard }, result.NextPlan.RemainingPhases);
        }

        // ---------- 정령 이름 ----------

        private CallResult<string> Rename(int relicId, string name) =>
            Run<string>((c, f) => _backend.RenameSpirit(relicId, name, c, f));

        /// <summary>유물 카탈로그가 null이면 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_NullCatalog_Throws()
        {
            RelicInventory inventory = new RelicInventory(ClarityPolicy.Default, 3, new OwnedRelic[0]);

            Assert.Throws<ArgumentNullException>(() =>
                new MockWorkbenchBackend(inventory, CreatePlanner(), 10, null, new Random(1), () => _now, 0f));
        }

        /// <summary>이름을 지은 적 없으면 작업대·상세 모두 기본 이름을 주는지 검사합니다.</summary>
        [Test]
        public void SpiritName_DefaultsToCatalogName()
        {
            CreateBackend(3, 10, CompletedDaysAgo(2, 1));

            Assert.AreEqual("정령2", Workbench().Value.Spirits[0].SpiritName);
            Assert.AreEqual("정령2", Detail(2).Value.SpiritName);
        }

        /// <summary>최초 정화 중인 유물의 상세에는 이름이 공개되지 않는지 검사합니다.</summary>
        [Test]
        public void SpiritName_HiddenWhileInProgress()
        {
            CreateBackend(3, 10, InProgress(1));

            Assert.IsNull(Detail(1).Value.SpiritName);
        }

        /// <summary>이름을 바꾸면 앞뒤 공백을 자른 이름이 저장되고 작업대·상세에 보이는지 검사합니다.</summary>
        [Test]
        public void Rename_TrimsAndStores()
        {
            CreateBackend(3, 10, CompletedDaysAgo(2, 1));

            CallResult<string> result = Rename(2, "  깨비  ");

            Assert.AreEqual("깨비", result.Value);
            Assert.AreEqual("깨비", Workbench().Value.Spirits[0].SpiritName);
            Assert.AreEqual("깨비", Detail(2).Value.SpiritName);
        }

        /// <summary>이름을 비워 보내면 기본 이름으로 되돌리고, 돌려받는 이름도 기본 이름인지 검사합니다.</summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Rename_EmptyResetsToDefault(string empty)
        {
            CreateBackend(3, 10, CompletedDaysAgo(2, 1));
            Rename(2, "깨비");

            CallResult<string> result = Rename(2, empty);

            Assert.AreEqual("정령2", result.Value);
            Assert.AreEqual("정령2", Detail(2).Value.SpiritName);
        }

        /// <summary>규칙에 어긋난 이름은 실패하고, 이전 이름이 그대로인지 검사합니다.</summary>
        [TestCase("깨")]
        [TestCase("열한글자가넘는정령이름")]
        public void Rename_InvalidLength_FailsAndKeepsName(string name)
        {
            CreateBackend(3, 10, CompletedDaysAgo(2, 1));
            Rename(2, "깨비");

            Assert.AreEqual(1, Rename(2, name).FailedCount);
            Assert.AreEqual("깨비", Detail(2).Value.SpiritName);
        }

        /// <summary>가진 유물이 아니거나 최초 정화 중이면 이름을 지을 수 없는지 검사합니다.</summary>
        [Test]
        public void Rename_NotOwnedOrInProgress_Fails()
        {
            CreateBackend(3, 10, InProgress(1));

            Assert.AreEqual(1, Rename(9, "깨비").FailedCount);
            Assert.AreEqual(1, Rename(1, "깨비").FailedCount);
        }

        /// <summary>최초 정화를 마치면 그때부터 기본 이름이 보이는지 검사합니다.</summary>
        [Test]
        public void SpiritName_AppearsAfterFirstPurification()
        {
            CreateBackend(3, 10, InProgress(1));
            for (int i = 0; i < 3; i++)
            {
                string token = Start(1, null).Value.SessionToken;
                Report(token, PlayResult.Success);
            }

            Assert.AreEqual("정령1", Detail(1).Value.SpiritName);
        }

        // ---------- 시작 설정·보고 내용 ----------

        /// <summary>정화를 시작하면 카탈로그의 봉인 매개체와 시드가 세션에 담기고, 미니게임 시작 설정으로 바뀌는지 검사합니다.</summary>
        [Test]
        public void Start_SessionCarriesSealMediumAndSeed()
        {
            CreateBackend(3, 10, InProgress(2));

            PurificationSession session = Start(2, null).Value;
            PlaySetup setup = session.ToSetup();

            Assert.AreEqual(SealMedium.Fog, session.SealMedium);
            Assert.AreEqual(2, setup.RelicId);
            Assert.AreEqual(Phase.Unseal, setup.Phase);
            Assert.AreEqual(MinigameDifficulty.Normal, setup.Difficulty);
            Assert.AreEqual(SealMedium.Fog, setup.SealMedium);
            Assert.AreEqual(session.Seed, setup.Seed);
        }

        /// <summary>카탈로그에 없는 유물은 봉인 매개체를 정할 수 없어 정화를 시작하지 못하는지 검사합니다.</summary>
        [Test]
        public void Start_UnknownRelic_Fails()
        {
            _inventory = new RelicInventory(ClarityPolicy.Default, 3, new[] { InProgress(1) });
            _random = new QueuedRandom();
            _backend = new MockWorkbenchBackend(_inventory, CreatePlanner(), 10, new TestCatalog(10, 1), _random, () => _now, 0f);

            CallResult<PurificationSession> result = Start(1, null);

            Assert.AreEqual(1, result.FailedCount);
            Assert.IsNull(result.Value);
        }

        /// <summary>모르는 정령의 이름은 예외 없이 "알 수 없는 유물"로 보이는지 검사합니다.</summary>
        [Test]
        public void SpiritName_UnknownRelic_ShowsUnknown()
        {
            _inventory = new RelicInventory(ClarityPolicy.Default, 3, new[] { CompletedDaysAgo(3, 1) });
            _random = new QueuedRandom();
            _backend = new MockWorkbenchBackend(_inventory, CreatePlanner(), 10, new TestCatalog(10, 3), _random, () => _now, 0f);

            Assert.AreEqual(WorkbenchText.UnknownRelic, Workbench().Value.Spirits[0].SpiritName);
        }

        /// <summary>같은 결과라도 보고 내용(사용 횟수 등)이 다르면 충돌로 거부하는지 검사합니다(서버 matches).</summary>
        [Test]
        public void Report_SameResultDifferentDetails_Fails()
        {
            OwnedRelic relic = InProgress(1);
            CreateBackend(3, 10, relic);
            string token = Start(1, null).Value.SessionToken;
            SettlementResult first = Report(token, new PlayReport(PlayResult.Success, null, 3, 1, 10, 2)).Value;

            Assert.AreEqual(1, Report(token, new PlayReport(PlayResult.Success, null, 3, 1, 10, 5)).FailedCount);
            Assert.AreSame(first, Report(token, new PlayReport(PlayResult.Success, null, 3, 1, 10, 2)).Value);
            Assert.AreEqual(Phase.Recall, relic.CurrentPhase);
        }

        // ---------- 전체 흐름 ----------

        /// <summary>최초 정화 세 페이즈를 마치면 슬롯이 비고 정령이 되며, 다시 상자를 열 수 있는지 검사합니다.</summary>
        [Test]
        public void FullFirstPurification_FreesSlotAndAllowsNextBox()
        {
            CreateBackend(1, 10, InProgress(1));
            Assert.IsTrue(Workbench().Value.IsFull);

            for (int i = 0; i < 3; i++)
            {
                string token = Start(1, null).Value.SessionToken;
                Assert.IsTrue(Report(token, PlayResult.Success).Succeeded);
            }

            WorkbenchSnapshot snapshot = Workbench().Value;
            Assert.AreEqual(0, snapshot.Slots.Count);
            CollectionAssert.AreEqual(new[] { 1 }, snapshot.Spirits.Select(s => s.RelicId));
            Assert.IsFalse(snapshot.IsFull);

            _random.Enqueue(2);
            Assert.AreEqual(AcquireResult.New, Open(BoxSource.Free, "next").Value.Result);
        }
    }
}
