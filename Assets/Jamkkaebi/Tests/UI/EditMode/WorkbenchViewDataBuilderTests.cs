using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;
using Jamkkaebi.Scripts.UI.Workbench;
using NUnit.Framework;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>백엔드 응답을 작업대 화면 값으로 바꾸는 규칙을 검사합니다.</summary>
    public sealed class WorkbenchViewDataBuilderTests
    {
        private static readonly DateTime LastPurifiedAt = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);

        private WorkbenchViewDataBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _builder = new WorkbenchViewDataBuilder(WorkbenchRelicSeed.Instance, IntimacyPolicy.Default, RestorePolicy.Default);
        }

        private static WorkbenchSnapshot Snapshot(int capacity, WorkbenchSlot[] slots, params WorkbenchSpirit[] spirits) =>
            new WorkbenchSnapshot(capacity, slots, spirits, 9 - slots.Length - spirits.Length);

        private static PlayPlan FirstPlan(Phase from)
        {
            Phase[] remaining = new[] { Phase.Unseal, Phase.Recall, Phase.Guard }.Where(p => p >= from).ToArray();
            return new PlayPlan(EntryRoute.First, remaining, new[] { MinigameDifficulty.Normal }, null);
        }

        private static RelicDetail InProgressDetail(int relicId, Phase current) =>
            new RelicDetail(relicId, RelicStatus.InProgress, null, null, null, null, 0, FirstPlan(current));

        private static RelicDetail SpiritDetail(int relicId, string name, Clarity clarity, Phase[] remaining,
            MinigameDifficulty[] allowed, int careCount) =>
            new RelicDetail(relicId, RelicStatus.Completed, name, clarity, LastPurifiedAt,
                clarity == Clarity.Resealed ? (DateTime?)null : LastPurifiedAt + TimeSpan.FromDays(14), careCount,
                new PlayPlan(EntryRoute.Care, remaining, allowed, clarity));

        // ---------- 생성자 ----------

        /// <summary>null 인자를 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new WorkbenchViewDataBuilder(null, IntimacyPolicy.Default, RestorePolicy.Default));
            Assert.Throws<ArgumentNullException>(() =>
                new WorkbenchViewDataBuilder(WorkbenchRelicSeed.Instance, null, RestorePolicy.Default));
            Assert.Throws<ArgumentNullException>(() =>
                new WorkbenchViewDataBuilder(WorkbenchRelicSeed.Instance, IntimacyPolicy.Default, null));
        }

        // ---------- 홈 ----------

        /// <summary>기억 회복 전 슬롯은 별칭, 뒤에는 실제 이름을 제목으로 쓰는지 검사합니다.</summary>
        [Test]
        public void Home_SlotTitleFollowsRevealLevel()
        {
            WorkbenchHomeData home = _builder.Home(Snapshot(3, new[]
            {
                new WorkbenchSlot(2, RevealLevel.Silhouette, Phase.Unseal),
                new WorkbenchSlot(5, RevealLevel.Form, Phase.Recall),
                new WorkbenchSlot(8, RevealLevel.Named, Phase.Guard)
            }));

            CollectionAssert.AreEqual(new[] { "백제, 향이 스민 산맥", "고려, 살결에 새긴 구름", "마패" },
                home.Slots.Select(s => s.Title));
            CollectionAssert.AreEqual(new[] { "삼국", "고려", "조선" }, home.Slots.Select(s => s.EraName));
            Assert.AreEqual(Phase.Recall, home.Slots[1].NextPhase);
        }

        /// <summary>빈칸이 있으면 등록 버튼을 보이고, 가득 차면 숨기며 안내 문구가 바뀌는지 검사합니다.</summary>
        [Test]
        public void Home_RegisterButtonAndGuide()
        {
            WorkbenchHomeData empty = _builder.Home(Snapshot(3, new WorkbenchSlot[0]));
            Assert.IsTrue(empty.CanRegister);
            Assert.AreEqual(WorkbenchText.RegisterNewRelic, empty.Guide);

            WorkbenchHomeData some = _builder.Home(Snapshot(3, new[] { new WorkbenchSlot(1, RevealLevel.Silhouette, Phase.Unseal) }));
            Assert.IsTrue(some.CanRegister);
            Assert.AreEqual(WorkbenchText.ChooseRelic, some.Guide);

            WorkbenchHomeData full = _builder.Home(Snapshot(1, new[] { new WorkbenchSlot(1, RevealLevel.Silhouette, Phase.Unseal) }));
            Assert.IsFalse(full.CanRegister);
            Assert.AreEqual(WorkbenchText.WorkbenchFull, full.Guide);
        }

        /// <summary>정령 카드는 응답의 정령 이름과 카탈로그의 실제 유물 이름을, 응답 순서대로 담는지 검사합니다.</summary>
        [Test]
        public void Home_SpiritCards()
        {
            WorkbenchHomeData home = _builder.Home(Snapshot(3, new WorkbenchSlot[0],
                new WorkbenchSpirit(9, "냥이", Clarity.Resealed, 2),
                new WorkbenchSpirit(2, "향산이", Clarity.Clear, 0)));

            CollectionAssert.AreEqual(new[] { 9, 2 }, home.Spirits.Select(s => s.RelicId));
            Assert.AreEqual("냥이", home.Spirits[0].SpiritName);
            Assert.AreEqual("묘작도", home.Spirits[0].RelicName);
            Assert.AreEqual("재봉인", home.Spirits[0].ClarityName);
        }

        /// <summary>모르는 relicId는 예외 없이 "알 수 없는 유물"로 보여 주는지 검사합니다.</summary>
        [Test]
        public void Home_UnknownRelic_ShownAsUnknown()
        {
            WorkbenchHomeData home = _builder.Home(Snapshot(3,
                new[] { new WorkbenchSlot(99, RevealLevel.Named, Phase.Guard) },
                new WorkbenchSpirit(98, "새정령", Clarity.Clear, 0)));

            Assert.AreEqual(WorkbenchText.UnknownRelic, home.Slots[0].Title);
            Assert.AreEqual(WorkbenchText.UnknownEra, home.Slots[0].EraName);
            Assert.IsFalse(home.Slots[0].IsKnown);
            Assert.AreEqual("새정령", home.Spirits[0].SpiritName);
            Assert.AreEqual(WorkbenchText.UnknownRelic, home.Spirits[0].RelicName);
        }

        // ---------- 진행 표시 ----------

        /// <summary>최초 정화 진행 표시: 앞은 완료, 지금은 현재, 뒤는 잠김.</summary>
        [TestCase(Phase.Unseal, new[] { PhaseStepState.Current, PhaseStepState.Locked, PhaseStepState.Locked })]
        [TestCase(Phase.Recall, new[] { PhaseStepState.Done, PhaseStepState.Current, PhaseStepState.Locked })]
        [TestCase(Phase.Guard, new[] { PhaseStepState.Done, PhaseStepState.Done, PhaseStepState.Current })]
        public void FirstRouteSteps(Phase current, PhaseStepState[] expected)
        {
            IReadOnlyList<PhaseStep> steps = WorkbenchViewDataBuilder.FirstRouteSteps(current);

            CollectionAssert.AreEqual(expected, steps.Select(s => s.State));
            CollectionAssert.AreEqual(new[] { "봉인 해제", "기억 회복", "오염 관리" }, steps.Select(s => s.Name));
        }

        /// <summary>관리 복구 진행 표시: 남은 순서에 없으면 완료, 남은 첫 페이즈는 현재, 나머지는 잠김.</summary>
        [TestCase(new[] { Phase.Guard }, new[] { PhaseStepState.Done, PhaseStepState.Done, PhaseStepState.Current })]
        [TestCase(new[] { Phase.Recall, Phase.Guard }, new[] { PhaseStepState.Done, PhaseStepState.Current, PhaseStepState.Locked })]
        [TestCase(new[] { Phase.Unseal, Phase.Recall, Phase.Guard }, new[] { PhaseStepState.Current, PhaseStepState.Locked, PhaseStepState.Locked })]
        public void RestoreSteps(Phase[] remaining, PhaseStepState[] expected)
        {
            CollectionAssert.AreEqual(expected, WorkbenchViewDataBuilder.RestoreSteps(remaining).Select(s => s.State));
        }

        // ---------- 유물 상세 ----------

        /// <summary>진행 중 유물 상세: 공개 단계에 맞는 제목, 시대, 진행 표시, 안내 문구.</summary>
        [Test]
        public void RelicDetail_InProgress()
        {
            RelicDetailData data = _builder.RelicDetail(InProgressDetail(2, Phase.Recall));

            Assert.AreEqual(RevealLevel.Form, data.RevealLevel);
            Assert.AreEqual("백제, 향이 스민 산맥", data.Title);
            Assert.AreEqual("삼국", data.EraName);
            Assert.AreEqual(WorkbenchText.CompleteAllToDisplay, data.Notice);
            CollectionAssert.AreEqual(new[] { PhaseStepState.Done, PhaseStepState.Current, PhaseStepState.Locked },
                data.Steps.Select(s => s.State));
            Assert.AreEqual(WorkbenchText.PurifyButton, data.PurifyButton);
        }

        /// <summary>기억 회복을 마치면 상세 제목이 실제 이름으로 바뀌는지 검사합니다.</summary>
        [Test]
        public void RelicDetail_AfterRecall_ShowsRealName()
        {
            Assert.AreEqual("금동대향로", _builder.RelicDetail(InProgressDetail(2, Phase.Guard)).Title);
        }

        /// <summary>완료한 정령을 유물 상세로 만들려고 하면 거부하는지 검사합니다.</summary>
        [Test]
        public void RelicDetail_Completed_Throws()
        {
            RelicDetail spirit = SpiritDetail(2, "향산이", Clarity.Clear, new[] { Phase.Guard },
                new[] { MinigameDifficulty.Easy, MinigameDifficulty.Normal, MinigameDifficulty.Hard }, 0);

            Assert.Throws<ArgumentException>(() => _builder.RelicDetail(spirit));
            Assert.Throws<ArgumentException>(() => _builder.SpiritDetail(InProgressDetail(2, Phase.Unseal), LastPurifiedAt));
        }

        // ---------- 정령 상세 ----------

        /// <summary>
        /// 흐려진 정령 상세: 이름, 실제 유물 이름, 시대, 선명도, 복구 진행, 경과일, 다음 페이즈 버튼, 난이도 선택지.
        /// </summary>
        [Test]
        public void SpiritDetail_Faded()
        {
            RelicDetail detail = SpiritDetail(8, "번개", Clarity.Faded, new[] { Phase.Recall, Phase.Guard },
                new[] { MinigameDifficulty.Normal, MinigameDifficulty.Hard }, 4);

            SpiritDetailData data = _builder.SpiritDetail(detail, LastPurifiedAt + TimeSpan.FromDays(7.5));

            Assert.AreEqual("번개", data.SpiritName);
            Assert.AreEqual("마패", data.RelicName);
            Assert.AreEqual("조선", data.EraName);
            Assert.AreEqual("흐려짐", data.ClarityName);
            CollectionAssert.AreEqual(new[] { PhaseStepState.Done, PhaseStepState.Current, PhaseStepState.Locked },
                data.RestoreSteps.Select(s => s.State));
            Assert.AreEqual("마지막 정화로부터 7일째", data.DaysSincePurified);
            Assert.AreEqual("유물 정화하기 - 기억 회복", data.PurifyButton);
            Assert.IsTrue(data.DifficultySelectable);
            CollectionAssert.AreEqual(new[] { "보통", "어려움" }, data.Difficulties.Select(d => d.Name));
            CollectionAssert.AreEqual(new[] { "한 단계 복구", "전체 복구" }, data.Difficulties.Select(d => d.EffectText));
        }

        /// <summary>친밀도는 돌봄 횟수로 계산하는지 검사합니다(기본 정책: 3회에 Lv.2).</summary>
        [Test]
        public void SpiritDetail_IntimacyFromCareCount()
        {
            RelicDetail detail = SpiritDetail(2, "향산이", Clarity.Clear, new[] { Phase.Guard },
                new[] { MinigameDifficulty.Easy, MinigameDifficulty.Normal, MinigameDifficulty.Hard }, 4);

            IntimacyGauge gauge = _builder.SpiritDetail(detail, LastPurifiedAt).Intimacy;

            Assert.AreEqual(IntimacyPolicy.Default.GaugeOf(4).Level, gauge.Level);
            Assert.AreEqual(2, gauge.Level);
        }

        /// <summary>같은 날이면 "오늘 정화했어요", 기기 시각이 앞서 있어도 음수 날수가 나오지 않는지 검사합니다.</summary>
        [Test]
        public void DaysSincePurified_Boundaries()
        {
            Assert.AreEqual(0, WorkbenchViewDataBuilder.DaysBetween(LastPurifiedAt, LastPurifiedAt + TimeSpan.FromHours(23)));
            Assert.AreEqual(1, WorkbenchViewDataBuilder.DaysBetween(LastPurifiedAt, LastPurifiedAt + TimeSpan.FromHours(24)));
            Assert.AreEqual(0, WorkbenchViewDataBuilder.DaysBetween(LastPurifiedAt, LastPurifiedAt - TimeSpan.FromDays(2)));
            Assert.AreEqual("오늘 정화했어요", WorkbenchText.DaysSincePurified(0));
            Assert.AreEqual("마지막 정화로부터 14일째", WorkbenchText.DaysSincePurified(14));
        }

        /// <summary>모르는 정령도 응답의 이름은 그대로 쓰고 유물 이름·시대만 대체하는지 검사합니다.</summary>
        [Test]
        public void SpiritDetail_UnknownRelic()
        {
            RelicDetail detail = SpiritDetail(99, "새정령", Clarity.Resealed, new[] { Phase.Unseal, Phase.Recall, Phase.Guard },
                new[] { MinigameDifficulty.Normal, MinigameDifficulty.Hard }, 0);

            SpiritDetailData data = _builder.SpiritDetail(detail, LastPurifiedAt + TimeSpan.FromDays(20));

            Assert.AreEqual("새정령", data.SpiritName);
            Assert.AreEqual(WorkbenchText.UnknownRelic, data.RelicName);
            Assert.AreEqual(WorkbenchText.UnknownEra, data.EraName);
            Assert.AreEqual("유물 정화하기 - 봉인 해제", data.PurifyButton);
        }

        // ---------- 서버 시드 ----------

        /// <summary>서버 시드와 같은 유물 9종이 relicId 1~9로 들어 있는지 검사합니다.</summary>
        [Test]
        public void Seed_HasServerRelics()
        {
            WorkbenchRelicSeed seed = WorkbenchRelicSeed.Instance;

            Assert.AreEqual(9, seed.Count);
            for (int id = 1; id <= 9; id++) Assert.IsTrue(seed.TryFind(id, out _), $"relicId {id}");
            Assert.IsFalse(seed.TryFind(10, out WorkbenchRelicInfo missing));
            Assert.IsNull(missing);

            Assert.IsTrue(seed.TryFind(2, out WorkbenchRelicInfo incense));
            Assert.AreEqual("금동대향로", incense.RealName);
            Assert.AreEqual("향산이", incense.DefaultSpiritName);
            Assert.IsTrue(seed.TryFind(7, out WorkbenchRelicInfo pin));
            Assert.AreEqual("봉 뒤꽂이", pin.RealName);
        }
    }
}
