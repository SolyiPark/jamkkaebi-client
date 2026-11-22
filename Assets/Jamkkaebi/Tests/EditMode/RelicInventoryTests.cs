using System;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Relic;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class RelicInventoryTests
    {
        private static readonly DateTime BaseTime = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Now = BaseTime + TimeSpan.FromDays(30);

        // 흐려짐 7일, 재봉인 14일
        private static readonly ClarityPolicy Policy =
            new ClarityPolicy(TimeSpan.FromDays(7), TimeSpan.FromDays(14));

        private static RelicInventory Create(int capacity, params OwnedRelic[] relics)
        {
            return new RelicInventory(Policy, capacity, relics);
        }

        /// <summary>최초 정화를 마친 유물. Now 기준으로 daysAgo일 전에 마지막으로 정화했다.</summary>
        private static OwnedRelic Completed(int relicId, double daysAgo)
        {
            return new OwnedRelic(relicId, BaseTime, RelicStatus.Completed, Phase.Guard,
                Now - TimeSpan.FromDays(daysAgo), Now - TimeSpan.FromDays(daysAgo), 0);
        }

        /// <summary>최초 정화 중인 유물. BaseTime에서 hoursLater시간 뒤에 획득했다.</summary>
        private static OwnedRelic InProgress(int relicId, double hoursLater = 0)
        {
            return OwnedRelic.Acquired(relicId, BaseTime + TimeSpan.FromHours(hoursLater));
        }

        /// <summary>서버 기본 작업대 용량이 3인지 검사합니다.</summary>
        [Test]
        public void DefaultWorkbenchCapacity_IsThree()
        {
            Assert.AreEqual(3, RelicInventory.DefaultWorkbenchCapacity);
        }

        /// <summary>null 정책·null 목록을 ArgumentNullException으로 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_NullArguments_ThrowArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => new RelicInventory(null, 3, new OwnedRelic[0]));
            Assert.Throws<ArgumentNullException>(() => new RelicInventory(Policy, 3, null));
        }

        /// <summary>용량이 1 미만이면 거부하는지 검사합니다.</summary>
        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_CapacityBelowOne_Throws(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(capacity));
        }

        /// <summary>목록에 null이나 같은 relicId가 있으면 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_NullOrDuplicateRelic_Throws()
        {
            Assert.Throws<ArgumentException>(() => Create(3, InProgress(1), null));
            Assert.Throws<ArgumentException>(() => Create(3, InProgress(1), Completed(1, 1)));
        }

        /// <summary>
        /// 진행 중 유물이 용량보다 많아도 불러오고, 가득 찬 것으로 보는지 검사합니다.
        /// (서버 설정이 줄어든 경우)
        /// </summary>
        [Test]
        public void Constructor_OverCapacity_AcceptedAndFull()
        {
            RelicInventory inventory = Create(2, InProgress(1), InProgress(2), InProgress(3));

            Assert.AreEqual(3, inventory.InProgressCount);
            Assert.IsTrue(inventory.IsWorkbenchFull);
            Assert.Throws<InvalidOperationException>(() => inventory.Acquire(4, Now));
        }

        /// <summary>생성 후 원본 배열을 바꿔도 목록이 바뀌지 않는지 검사합니다.</summary>
        [Test]
        public void Constructor_CopiesRelics()
        {
            OwnedRelic[] source = { InProgress(1) };
            RelicInventory inventory = new RelicInventory(Policy, 3, source);

            source[0] = InProgress(2);

            Assert.IsTrue(inventory.Contains(1));
            Assert.IsFalse(inventory.Contains(2));
        }

        /// <summary>Count·Contains·TryGet이 담긴 유물을 찾는지 검사합니다.</summary>
        [Test]
        public void Lookup_FindsOwnedRelics()
        {
            OwnedRelic first = InProgress(1);
            RelicInventory inventory = Create(3, first, Completed(5, 1));

            Assert.AreEqual(2, inventory.Count);
            Assert.IsTrue(inventory.Contains(5));
            Assert.IsFalse(inventory.Contains(9));

            Assert.IsTrue(inventory.TryGet(1, out OwnedRelic found));
            Assert.AreSame(first, found);
            Assert.IsFalse(inventory.TryGet(9, out OwnedRelic missing));
            Assert.IsNull(missing);
        }

        /// <summary>RelicIds가 가진 유물의 relicId를 모두 담고, 획득하면 늘어나는지 검사합니다.</summary>
        [Test]
        public void RelicIds_ListsOwnedIds()
        {
            RelicInventory inventory = Create(3, InProgress(4), Completed(9, 1));

            CollectionAssert.AreEquivalent(new[] { 4, 9 }, inventory.RelicIds);

            inventory.Acquire(2, Now);

            CollectionAssert.AreEquivalent(new[] { 2, 4, 9 }, inventory.RelicIds);
        }

        /// <summary>생성자에 넘긴 선명도 정책을 그대로 돌려주는지 검사합니다.</summary>
        [Test]
        public void ClarityPolicy_ReturnsGivenPolicy()
        {
            Assert.AreSame(Policy, Create(3).ClarityPolicy);
        }

        /// <summary>진행 중 유물만 세고, 용량에 닿으면 가득 찬 것으로 보는지 검사합니다.</summary>
        [Test]
        public void InProgressCount_CountsOnlyInProgress()
        {
            RelicInventory twoOfThree = Create(3, InProgress(1), InProgress(2), Completed(3, 1), Completed(4, 20));
            RelicInventory threeOfThree = Create(3, InProgress(1), InProgress(2), InProgress(3), Completed(4, 1));

            Assert.AreEqual(2, twoOfThree.InProgressCount);
            Assert.IsFalse(twoOfThree.IsWorkbenchFull);
            Assert.AreEqual(3, threeOfThree.InProgressCount);
            Assert.IsTrue(threeOfThree.IsWorkbenchFull);
        }

        /// <summary>재봉인된 정령은 슬롯을 차지하지 않는지 검사합니다.</summary>
        [Test]
        public void IsWorkbenchFull_ResealedSpiritsDoNotTakeSlots()
        {
            RelicInventory inventory = Create(1, Completed(1, 20), Completed(2, 30));

            Assert.AreEqual(0, inventory.InProgressCount);
            Assert.IsFalse(inventory.IsWorkbenchFull);
        }

        /// <summary>작업대 슬롯이 진행 중 유물만 획득 순서로, 같으면 relicId 순으로 담는지 검사합니다.</summary>
        [Test]
        public void WorkbenchSlots_InProgressByAcquiredThenId()
        {
            RelicInventory inventory = Create(5,
                InProgress(7, 2), Completed(1, 1), InProgress(4, 0), InProgress(2, 0));

            CollectionAssert.AreEqual(new[] { 2, 4, 7 },
                inventory.WorkbenchSlots().Select(r => r.RelicId));
        }

        /// <summary>정령 목록이 완료 유물만 재봉인 → 흐려짐 → 선명, 같으면 relicId 순으로 담는지 검사합니다.</summary>
        [Test]
        public void SpiritsByCareOrder_ResealedFadedClearThenId()
        {
            RelicInventory inventory = Create(3,
                Completed(1, 1),   // 선명
                Completed(2, 10),  // 흐려짐
                Completed(3, 20),  // 재봉인
                Completed(4, 3),   // 선명
                Completed(5, 15),  // 재봉인
                InProgress(6));

            CollectionAssert.AreEqual(new[] { 3, 5, 2, 1, 4 },
                inventory.SpiritsByCareOrder(Now).Select(r => r.RelicId));
        }

        /// <summary>같은 유물이라도 now에 따라 정렬이 바뀌는지 검사합니다.</summary>
        [Test]
        public void SpiritsByCareOrder_DependsOnNow()
        {
            RelicInventory inventory = Create(3, Completed(1, 1), Completed(2, 3));

            // 지금은 둘 다 선명 → relicId 순
            CollectionAssert.AreEqual(new[] { 1, 2 },
                inventory.SpiritsByCareOrder(Now).Select(r => r.RelicId));

            // 5일 뒤: 1번은 6일(선명), 2번은 8일(흐려짐) → 2번이 먼저
            CollectionAssert.AreEqual(new[] { 2, 1 },
                inventory.SpiritsByCareOrder(Now + TimeSpan.FromDays(5)).Select(r => r.RelicId));
        }

        /// <summary>돌려준 목록을 바깥에서 바꿀 수 없는지 검사합니다.</summary>
        [Test]
        public void Lists_AreReadOnly()
        {
            RelicInventory inventory = Create(3, InProgress(1), Completed(2, 1));

            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.Generic.IList<OwnedRelic>)inventory.WorkbenchSlots()).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.Generic.IList<OwnedRelic>)inventory.SpiritsByCareOrder(Now)).Clear());
        }

        /// <summary>새 유물이면 봉인 해제부터인 진행 중 유물로 추가되는지 검사합니다.</summary>
        [Test]
        public void Acquire_New_AddsInProgressRelic()
        {
            RelicInventory inventory = Create(3);

            AcquireResult result = inventory.Acquire(8, Now);

            Assert.AreEqual(AcquireResult.New, result);
            Assert.IsTrue(inventory.TryGet(8, out OwnedRelic relic));
            Assert.AreEqual(RelicStatus.InProgress, relic.Status);
            Assert.AreEqual(Phase.Unseal, relic.CurrentPhase);
            Assert.AreEqual(Now, relic.AcquiredAt);
            Assert.AreEqual(1, inventory.InProgressCount);
        }

        /// <summary>이미 가진 유물이면 Duplicate이고 목록·유물 상태가 그대로인지 검사합니다.</summary>
        [Test]
        public void Acquire_Duplicate_KeepsInventory()
        {
            OwnedRelic owned = Completed(2, 20);
            RelicInventory inventory = Create(3, owned);

            AcquireResult result = inventory.Acquire(2, Now);

            Assert.AreEqual(AcquireResult.Duplicate, result);
            Assert.AreEqual(1, inventory.Count);
            Assert.IsTrue(inventory.TryGet(2, out OwnedRelic found));
            Assert.AreSame(owned, found);
            Assert.AreEqual(RelicStatus.Completed, found.Status);
            Assert.AreEqual(0, inventory.InProgressCount);
        }

        /// <summary>작업대가 가득 차면 새 유물을 막고 목록을 바꾸지 않는지 검사합니다.</summary>
        [Test]
        public void Acquire_WhenFull_ThrowsAndKeepsInventory()
        {
            RelicInventory inventory = Create(2, InProgress(1), InProgress(2));

            Assert.Throws<InvalidOperationException>(() => inventory.Acquire(3, Now));
            Assert.AreEqual(2, inventory.Count);
            Assert.IsFalse(inventory.Contains(3));
        }

        /// <summary>작업대가 가득 차면 중복 유물도 막는지 검사합니다(서버는 상자를 열기 전에 막는다).</summary>
        [Test]
        public void Acquire_WhenFull_RejectsDuplicateToo()
        {
            RelicInventory inventory = Create(1, InProgress(1), Completed(2, 1));

            Assert.Throws<InvalidOperationException>(() => inventory.Acquire(1, Now));
            Assert.Throws<InvalidOperationException>(() => inventory.Acquire(2, Now));
        }

        /// <summary>새 유물로 마지막 슬롯을 채우면 그다음 뽑기가 막히는지 검사합니다.</summary>
        [Test]
        public void Acquire_FillsLastSlot_ThenBlocks()
        {
            RelicInventory inventory = Create(2, InProgress(1));

            Assert.AreEqual(AcquireResult.New, inventory.Acquire(2, Now));
            Assert.IsTrue(inventory.IsWorkbenchFull);
            Assert.Throws<InvalidOperationException>(() => inventory.Acquire(3, Now));
        }

        /// <summary>최초 정화를 마치면 슬롯이 비어 다시 뽑을 수 있는지 검사합니다.</summary>
        [Test]
        public void Acquire_AfterFirstPurification_SlotFreed()
        {
            OwnedRelic relic = InProgress(1);
            RelicInventory inventory = Create(1, relic);
            Assert.IsTrue(inventory.IsWorkbenchFull);

            relic.ClearFirstPlay(Now); // 봉인 해제
            relic.ClearFirstPlay(Now); // 기억 회복
            relic.ClearFirstPlay(Now); // 오염 관리 → 완료

            Assert.IsFalse(inventory.IsWorkbenchFull);
            Assert.AreEqual(AcquireResult.New, inventory.Acquire(2, Now));
        }
    }
}
