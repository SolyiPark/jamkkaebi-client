using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Npc;
using Jamkkaebi.Scripts.Gameplay.Relic;
using NUnit.Framework;

/// <summary>
/// 대사 패키지의 친밀도(NpcAddress.IntimacyOf)가 게임의 친밀도 규칙(IntimacyPolicy)과 같은 값을 내는지,
/// 주인 장면은 관리 성공 횟수로, 손님 장면은 최근 30일 방문한 날 수로 호칭을 정하는지 확인한다.
/// </summary>
public sealed class NpcIntimacyTests
{
    private const int RelicId = 5;
    private const string Nickname = "도윤";

    private static readonly DateTime Today = new DateTime(2026, 11, 25);

    private NpcContextBuilder _builder;

    [SetUp]
    public void SetUp()
    {
        var personas = NpcContentLoader.FromResources<PersonaCatalog>("Npc/personas");
        Assert.That(personas, Is.Not.Null, "Resources/Npc/personas.json을 읽지 못했습니다.");
        _builder = new NpcContextBuilder(personas);
    }

    [Test]
    public void LevelUpCareCounts_MatchDefaultIntimacyPolicy()
    {
        Assert.That(NpcAddress.LevelUpCareCounts, Is.EqualTo(IntimacyPolicy.Default.LevelUpCareCounts));
    }

    /// <summary>게임 화면의 Lv.1~5가 대사 패키지의 0~4단계와 한 단계씩 맞물리는지 검사합니다.</summary>
    [Test]
    public void IntimacyOf_IsOneBelowPolicyLevel()
    {
        int last = IntimacyPolicy.Default.LevelUpCareCounts.Last();
        for (int careCount = 0; careCount <= last + 5; careCount++)
        {
            Assert.That(NpcAddress.IntimacyOf(careCount), Is.EqualTo(IntimacyPolicy.Default.GaugeOf(careCount).Level - 1),
                "관리 " + careCount + "회");
        }
    }

    [Test]
    public void IntimacyOf_UnknownCareCount_IsZero()
    {
        Assert.That(NpcAddress.IntimacyOf(null), Is.EqualTo(0));
    }

    [TestCase(0, 0)]
    [TestCase(2, 0)]
    [TestCase(3, 1)]
    [TestCase(7, 2)]
    [TestCase(11, 2)]
    [TestCase(12, 3)]
    [TestCase(20, 4)]
    [TestCase(100, 4)]
    public void Owner_IntimacyFromCareCount(int careCount, int level)
    {
        ReactionRequest request = _builder.Build(Owner(careCount, null)).Request;

        Assert.That(request.Intimacy, Is.EqualTo(level));
        Assert.That(request.Familiar, Is.EqualTo(level >= NpcAddress.NicknameLevel));
        Assert.That(request.AddressName, Is.EqualTo(level >= NpcAddress.NicknameLevel ? Nickname : NpcAddress.Stranger));
    }

    [Test]
    public void Owner_IgnoresVisitDays()
    {
        ReactionRequest manyVisits = _builder.Build(Owner(2, Days(20))).Request;
        ReactionRequest noVisits = _builder.Build(Owner(12, new List<DateTime>())).Request;

        Assert.That(manyVisits.Intimacy, Is.EqualTo(0));
        Assert.That(manyVisits.AddressName, Is.EqualTo(NpcAddress.Stranger));
        Assert.That(noVisits.Intimacy, Is.EqualTo(3));
        Assert.That(noVisits.AddressName, Is.EqualTo(Nickname));
    }

    [Test]
    public void Owner_UnknownCareCount_IsStranger()
    {
        ReactionRequest request = _builder.Build(Owner(null, Days(20))).Request;

        Assert.That(request.Intimacy, Is.EqualTo(0));
        Assert.That(request.AddressName, Is.EqualTo(NpcAddress.Stranger));
    }

    [Test]
    public void Visitor_LevelFromVisitDays_IgnoresCareCount()
    {
        ReactionRequest fewVisits = _builder.Build(Visitor(20, Days(2))).Request;
        ReactionRequest manyVisits = _builder.Build(Visitor(0, Days(12))).Request;

        Assert.That(fewVisits.Intimacy, Is.EqualTo(0));
        Assert.That(fewVisits.AddressName, Is.EqualTo(NpcAddress.Stranger));
        Assert.That(manyVisits.Intimacy, Is.EqualTo(3));
        Assert.That(manyVisits.AddressName, Is.EqualTo(Nickname));
    }

    [Test]
    public void PrefetchRequests_KeepCareCount()
    {
        NpcContextInput state = Owner(12, null);
        state.Clarity = "CLEAR";

        List<ReactionRequest> requests = _builder.PrefetchRequests(state);

        Assert.That(requests, Is.Not.Empty);
        Assert.That(requests.Select(r => r.Intimacy), Is.All.EqualTo(3));
    }

    private static NpcContextInput Owner(int? careCount, List<DateTime> visitDates)
    {
        return new NpcContextInput
        {
            RelicId = RelicId, Mode = InteractionMode.Owner, Input = NpcInputs.Tap, Today = Today,
            Nickname = Nickname, CareCount = careCount, VisitDates = visitDates
        };
    }

    private static NpcContextInput Visitor(int? careCount, List<DateTime> visitDates)
    {
        return new NpcContextInput
        {
            RelicId = RelicId, Mode = InteractionMode.Visitor, Input = NpcInputs.Visit, Today = Today,
            Nickname = Nickname, CareCount = careCount, VisitDates = visitDates
        };
    }

    // 오늘부터 하루씩 거슬러 count일
    private static List<DateTime> Days(int count)
    {
        return Enumerable.Range(0, count).Select(i => Today.AddDays(-i)).ToList();
    }
}
