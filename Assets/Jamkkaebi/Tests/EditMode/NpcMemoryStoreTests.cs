using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jamkkaebi.Npc;
using Jamkkaebi.Scripts.Gameplay.Npc.Memory;
using NUnit.Framework;

/// <summary>
/// 대화 기억 저장소와 대사 패키지의 기억 꺼내기 규칙(오래된 것부터 한 번씩, 평소 안부와 번갈아)이 맞물리는지 확인한다.
/// SLM 없이 규칙 기반 생성기의 기본 대사로 반응을 띄운다.
/// </summary>
public sealed class NpcMemoryStoreTests
{
    private const int RelicId = 5;
    private const string FirstStory = "주인에게 첫 번째 이야기를 들려줬다";
    private const string SecondStory = "주인에게 두 번째 이야기를 들려줬다";
    private const string ThirdStory = "주인에게 세 번째 이야기를 들려줬다";

    private static readonly DateTime Today = new DateTime(2026, 11, 24);

    private NpcContextBuilder _builder;
    private NpcRequestManager _manager;
    private NpcMemoryStore _store;

    [SetUp]
    public void SetUp()
    {
        var personas = NpcContentLoader.FromResources<PersonaCatalog>("Npc/personas");
        var lines = NpcContentLoader.FromResources<DefaultLineCatalog>("Npc/default_lines");
        Assert.That(personas, Is.Not.Null, "Resources/Npc/personas.json을 읽지 못했습니다.");
        Assert.That(lines, Is.Not.Null, "Resources/Npc/default_lines.json을 읽지 못했습니다.");

        _builder = new NpcContextBuilder(personas);
        _manager = new NpcRequestManager(null, new RuleBasedReactionGenerator(lines, new Random(1)));
        _store = new NpcMemoryStore();
    }

    [Test]
    public void For_ReturnsSavedOrderWithDaysAgoAndRecalled()
    {
        _store.Add(RelicId, new MemoryEntry(0, NpcActions.TellStory, FirstStory), Today.AddDays(-3));
        _store.Add(RelicId, new MemoryEntry(0, NpcActions.TellStory, SecondStory) { Recalled = true }, Today.AddDays(-1));

        List<MemoryEntry> memory = _store.For(RelicId, Today);

        Assert.That(memory.Select(m => m.Summary), Is.EqualTo(new[] { FirstStory, SecondStory }));
        Assert.That(memory.Select(m => m.DaysAgo), Is.EqualTo(new[] { 3, 1 }));
        Assert.That(memory.Select(m => m.Recalled), Is.EqualTo(new[] { false, true }));
        Assert.That(_store.For(RelicId + 1, Today), Is.Empty);
    }

    [Test]
    public void For_SavedAfterToday_CountsAsToday()
    {
        _store.Add(RelicId, new MemoryEntry(0, NpcActions.TellStory, FirstStory), Today.AddDays(2));

        Assert.That(_store.For(RelicId, Today).Single().DaysAgo, Is.EqualTo(0));
    }

    [Test]
    public void MarkRecalled_EntryNotHandedOut_ReturnsFalse()
    {
        _store.Add(RelicId, new MemoryEntry(0, NpcActions.TellStory, FirstStory), Today);
        MemoryEntry handedOut = _store.For(RelicId, Today).Single();

        Assert.That(_store.MarkRecalled(RelicId, new MemoryEntry(0, NpcActions.TellStory, FirstStory)), Is.False);
        Assert.That(_store.MarkRecalled(RelicId + 1, handedOut), Is.False);
        Assert.That(_store.MarkRecalled(RelicId, null), Is.False);
        Assert.That(_store.RecordsOf(RelicId).Single().Recalled, Is.False);

        Assert.That(_store.MarkRecalled(RelicId, handedOut), Is.True);
        Assert.That(_store.RecordsOf(RelicId).Single().Recalled, Is.True);
    }

    [Test]
    public async Task AskStatus_RecallsOldestFirstAndAlternatesWithUsualAnswer()
    {
        // 같은 날 기억이면 먼저 저장한 것부터 꺼낸다.
        _store.Add(RelicId, new MemoryEntry(0, NpcActions.TellStory, FirstStory), Today.AddDays(-3));
        _store.Add(RelicId, new MemoryEntry(0, NpcActions.TellStory, SecondStory), Today.AddDays(-3));
        _store.Add(RelicId, new MemoryEntry(0, NpcActions.TellStory, ThirdStory), Today.AddDays(-1));

        var recalled = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            recalled.Add(await AskStatusAsync(_store));
        }

        Assert.That(recalled, Is.EqualTo(new[] { FirstStory, null, SecondStory, null, ThirdStory, null, null, null }));
    }

    [Test]
    public async Task AskStatus_Shown_MarksRecalledAndAppendsRecallTalkLast()
    {
        _store.Add(RelicId, new MemoryEntry(0, NpcActions.TellStory, FirstStory), Today.AddDays(-2));

        await AskStatusAsync(_store);

        IReadOnlyList<NpcMemoryRecord> records = _store.RecordsOf(RelicId);
        Assert.That(records.Count, Is.EqualTo(2));
        Assert.That(records[0].Recalled, Is.True);
        Assert.That(records[1].ActionId, Is.EqualTo(NpcActions.RecallTalk));
        Assert.That(records[1].SavedOn, Is.EqualTo(Today));
        Assert.That(records[1].Recalled, Is.False);

        // 다음 안부는 평소대로 답하고, 그때 남겼던 recall_talk 기억에 표시가 남는다.
        await AskStatusAsync(_store);

        Assert.That(_store.RecordsOf(RelicId).Count, Is.EqualTo(2));
        Assert.That(_store.RecordsOf(RelicId)[1].Recalled, Is.True);
    }

    [Test]
    public async Task AskStatus_NotShown_LeavesStoreAndRecallsSameMemoryNextTime()
    {
        _store.Add(RelicId, new MemoryEntry(0, NpcActions.TellStory, FirstStory), Today.AddDays(-2));

        NpcContext context = Build(_store);
        Assert.That(context.KeyFact, Is.EqualTo("recall"));
        bool changed = NpcMemoryRecorder.Record(_store, RelicId, context, null, Today);

        Assert.That(changed, Is.False);
        Assert.That(context.MemoryToMark, Is.Null);
        Assert.That(_store.RecordsOf(RelicId).Count, Is.EqualTo(1));
        Assert.That(_store.RecordsOf(RelicId)[0].Recalled, Is.False);
        Assert.That(await AskStatusAsync(_store), Is.EqualTo(FirstStory));
    }

    [Test]
    public async Task Archive_RoundTrip_KeepsOrderAndRecalled()
    {
        _store.Add(RelicId, new MemoryEntry(0, NpcActions.TellStory, FirstStory), Today.AddDays(-3));
        _store.Add(RelicId, new MemoryEntry(0, NpcActions.TellStory, SecondStory), Today.AddDays(-1));
        _store.Add(RelicId + 1, new MemoryEntry(0, NpcActions.TellStory, ThirdStory), Today);
        await AskStatusAsync(_store);

        var archive = new NpcMemoryArchive(new TextStore());
        archive.Save(_store);
        Assert.That(archive.TryLoad(out NpcMemoryStore loaded), Is.True);

        Assert.That(loaded.RelicIds, Is.EquivalentTo(new[] { RelicId, RelicId + 1 }));
        Assert.That(loaded.RecordsOf(RelicId).Select(r => r.Summary),
            Is.EqualTo(_store.RecordsOf(RelicId).Select(r => r.Summary)));
        Assert.That(loaded.RecordsOf(RelicId).Select(r => r.SavedOn),
            Is.EqualTo(new[] { Today.AddDays(-3), Today.AddDays(-1), Today }));
        Assert.That(loaded.RecordsOf(RelicId).Select(r => r.Recalled), Is.EqualTo(new[] { true, false, false }));

        // 앱을 다시 켠 뒤에도 꺼낸 기억은 다시 꺼내지 않고, 평소 안부 다음에 그다음 기억을 꺼낸다.
        Assert.That(await AskStatusAsync(loaded), Is.Null);
        Assert.That(await AskStatusAsync(loaded), Is.EqualTo(SecondStory));
    }

    [Test]
    public void Archive_OldDataWithoutRecalled_ReadsAsFalse()
    {
        var file = new TextStore
        {
            Text = "{\"version\":1,\"spirits\":[{\"relicId\":5,\"memories\":["
                   + "{\"savedOn\":\"2026-11-22\",\"actionId\":\"tell_story\",\"summary\":\"" + FirstStory + "\"},"
                   + "{\"savedOn\":\"날짜 아님\",\"actionId\":\"tell_story\",\"summary\":\"" + SecondStory + "\"}]}]}"
        };

        Assert.That(new NpcMemoryArchive(file).TryLoad(out NpcMemoryStore loaded), Is.True);

        NpcMemoryRecord record = loaded.RecordsOf(RelicId).Single();
        Assert.That(record.Summary, Is.EqualTo(FirstStory));
        Assert.That(record.SavedOn, Is.EqualTo(new DateTime(2026, 11, 22)));
        Assert.That(record.Recalled, Is.False);
    }

    [Test]
    public void Archive_NothingSaved_LoadsEmptyStore()
    {
        Assert.That(new NpcMemoryArchive(new TextStore()).TryLoad(out NpcMemoryStore loaded), Is.True);
        Assert.That(loaded.RelicIds, Is.Empty);
    }

    [Test]
    public void Archive_BrokenText_ReturnsFalseWithEmptyStore()
    {
        var file = new TextStore { Text = "{\"version\":1,\"spirits\":[" };

        Assert.That(new NpcMemoryArchive(file).TryLoad(out NpcMemoryStore loaded), Is.False);
        Assert.That(loaded.RelicIds, Is.Empty);
    }

    private NpcContext Build(NpcMemoryStore store)
    {
        // 선명하고 최근 관리가 없는 주인 — 안부가 지난 대화 아니면 평소 안부가 된다.
        return _builder.Build(new NpcContextInput
        {
            RelicId = RelicId,
            Mode = InteractionMode.Owner,
            Input = NpcInputs.AskStatus,
            Today = Today,
            Clarity = "CLEAR",
            AwakeningStage = "AWAKENED",
            LastPurifiedAt = null,
            Memory = store.For(RelicId, Today)
        });
    }

    /// <summary>안부를 한 번 묻고 반응을 띄운 것으로 마무리한다.</summary>
    /// <returns>이번에 꺼낸 기억의 요약. 평소 안부였으면 null.</returns>
    private async Task<string> AskStatusAsync(NpcMemoryStore store)
    {
        NpcContext context = Build(store);
        string recalled = context.KeyFact == "recall" ? context.MemoryToMark.Summary : null;

        NpcResponse response = await _manager.RequestAsync(context.Request);
        Assert.That(response.ShouldShow, Is.True, response.Note);
        NpcMemoryRecorder.Record(store, RelicId, context, response, Today);
        return recalled;
    }

    private sealed class TextStore : INpcLineStore
    {
        public string Text { get; set; }

        public string Read() => Text;

        public void Write(string text) => Text = text;
    }
}
