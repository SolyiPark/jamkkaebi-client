using System;
using System.Collections;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;
using Jamkkaebi.Scripts.UI.Shell;
using Jamkkaebi.Scripts.UI.Workbench;
using NUnit.Framework;
using UnityEditor;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>
    /// 작업대 relicId를 도감 종류 ID로 바꿔 도감 기록에 등록하는지 검사합니다.
    /// 실제 도감 카탈로그와 작업대 유물 목록을 써서 둘의 대응까지 함께 확인합니다.
    /// </summary>
    public sealed class CollectionRecorderTests
    {
        private const string CatalogPath = "Assets/Jamkkaebi/Configuration/CollectionCatalog.asset";

        private CollectionProgress _progress;
        private CollectionRecorder _recorder;
        private int _changedCount;

        [SetUp]
        public void SetUp()
        {
            CollectionCatalog catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath + "를 찾지 못했습니다.");

            _progress = new CollectionProgress(catalog);
            _progress.Changed += () => _changedCount++;
            _changedCount = 0;
            _recorder = new CollectionRecorder(_progress, WorkbenchRelicSeed.Instance);
        }

        private static string CollectionIdOf(int relicId)
        {
            Assert.That(WorkbenchRelicSeed.Instance.TryFind(relicId, out WorkbenchRelicInfo info), Is.True);
            return info.CollectionId;
        }

        /// <summary>작업대에서 막 완료한 정령은 도감 ID로 등록되고 신규(N)로 남는지 검사합니다.</summary>
        [Test]
        public void RegisterCompleted_NotSeen_RegistersAsUnread()
        {
            string collectionId = CollectionIdOf(8);

            Assert.That(_recorder.RegisterCompleted(8, alreadySeen: false), Is.True);

            Assert.That(_progress.IsRestored(collectionId), Is.True);
            Assert.That(_progress.IsUnread(collectionId), Is.True);
            Assert.That(_progress.RestoredCount, Is.EqualTo(1));
        }

        /// <summary>시작할 때 이미 완료돼 있던 정령은 등록하되 신규 표시 없이 확인한 상태가 되는지 검사합니다.</summary>
        [Test]
        public void RegisterCompleted_AlreadySeen_RegistersAsViewed()
        {
            string collectionId = CollectionIdOf(2);

            Assert.That(_recorder.RegisterCompleted(2, alreadySeen: true), Is.True);

            Assert.That(_progress.IsRestored(collectionId), Is.True);
            Assert.That(_progress.IsUnread(collectionId), Is.False);
        }

        /// <summary>
        /// 이미 등록한 유물은 다시 등록하지 않고 false를 돌려주며, 신규 상태도 바꾸지 않는지 검사합니다.
        /// (확인한 상태로 채운 정령을 다시 등록해도 N이 다시 생기지 않고, 신규인 정령을 alreadySeen으로 다시 불러도 N이 지워지지 않는다)
        /// </summary>
        [Test]
        public void RegisterCompleted_AlreadyRegistered_ReturnsFalseAndKeepsUnreadState()
        {
            _recorder.RegisterCompleted(2, alreadySeen: true);
            _recorder.RegisterCompleted(8, alreadySeen: false);
            _changedCount = 0;

            Assert.That(_recorder.RegisterCompleted(2, alreadySeen: false), Is.False);
            Assert.That(_recorder.RegisterCompleted(8, alreadySeen: true), Is.False);

            Assert.That(_progress.IsUnread(CollectionIdOf(2)), Is.False);
            Assert.That(_progress.IsUnread(CollectionIdOf(8)), Is.True);
            Assert.That(_progress.RestoredCount, Is.EqualTo(2));
            Assert.That(_changedCount, Is.Zero);
        }

        /// <summary>작업대 목록에 없는 relicId는 등록하지 않고 false를 돌려주는지 검사합니다.</summary>
        [TestCase(0)]
        [TestCase(99)]
        public void RegisterCompleted_UnknownRelicId_ReturnsFalse(int relicId)
        {
            Assert.That(_recorder.RegisterCompleted(relicId, alreadySeen: false), Is.False);

            Assert.That(_progress.RestoredCount, Is.Zero);
            Assert.That(_changedCount, Is.Zero);
        }

        /// <summary>작업대 유물의 도감 ID가 카탈로그에 없으면 등록하지 않고 false를 돌려주는지 검사합니다.</summary>
        [Test]
        public void RegisterCompleted_CollectionIdNotInCatalog_ReturnsFalse()
        {
            CollectionRecorder recorder = new CollectionRecorder(_progress, new MissingCollectionCatalog());

            Assert.That(recorder.RegisterCompleted(1, alreadySeen: true), Is.False);

            Assert.That(_progress.RestoredCount, Is.Zero);
            Assert.That(_changedCount, Is.Zero);
        }

        /// <summary>시작 시 채우기처럼 여러 정령을 등록하면 각각 도감에 들어가는지 검사합니다(데모 정령 2·8·9번).</summary>
        [Test]
        public void RegisterCompleted_DemoSpirits_RegistersThree()
        {
            foreach (int relicId in new[] { 2, 8, 9 })
                Assert.That(_recorder.RegisterCompleted(relicId, alreadySeen: true), Is.True, $"relicId {relicId}");

            Assert.That(_progress.RestoredCount, Is.EqualTo(3));
        }

        /// <summary>기록이나 유물 목록이 null이면 거부하는지 검사합니다.</summary>
        [Test]
        public void Constructor_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CollectionRecorder(null, WorkbenchRelicSeed.Instance));
            Assert.Throws<ArgumentNullException>(() => new CollectionRecorder(_progress, null));
        }

        // ---------- 작업대 조회로 누락 채우기 ----------

        /// <summary>처음 받은 정령 목록은 시작 상태로 보고 신규 표시 없이 등록하는지 검사합니다.</summary>
        [Test]
        public void SyncCompleted_FirstSnapshot_RegistersAsViewed()
        {
            Assert.That(_recorder.SyncCompleted(new[] { 2, 8, 9 }), Is.EqualTo(3));

            Assert.That(_progress.RestoredCount, Is.EqualTo(3));
            foreach (int relicId in new[] { 2, 8, 9 })
                Assert.That(_progress.IsUnread(CollectionIdOf(relicId)), Is.False, $"relicId {relicId}");
        }

        /// <summary>
        /// 시작 상태 뒤의 목록에서 새로 보이는 정령은 신규(N)로 등록하고, 이미 등록된 정령은 그대로 두는지 검사합니다.
        /// (정산 응답을 잃고 홈으로 나온 뒤 작업대를 다시 조회한 경우)
        /// </summary>
        [Test]
        public void SyncCompleted_LaterSnapshot_RegistersMissingAsUnread()
        {
            _recorder.SyncCompleted(new[] { 2, 8, 9 });
            _changedCount = 0;

            Assert.That(_recorder.SyncCompleted(new[] { 2, 7, 8, 9 }), Is.EqualTo(1));

            Assert.That(_progress.RestoredCount, Is.EqualTo(4));
            Assert.That(_progress.IsUnread(CollectionIdOf(7)), Is.True);
            Assert.That(_progress.IsUnread(CollectionIdOf(2)), Is.False);
            Assert.That(_changedCount, Is.EqualTo(1));
        }

        /// <summary>시작 상태 목록이 비어 있어도 시작 상태로 치고, 그 뒤 정령은 신규로 등록하는지 검사합니다.</summary>
        [Test]
        public void SyncCompleted_EmptyFirstSnapshot_StillSetsBaseline()
        {
            Assert.That(_recorder.SyncCompleted(Array.Empty<int>()), Is.Zero);

            _recorder.SyncCompleted(new[] { 7 });

            Assert.That(_progress.IsUnread(CollectionIdOf(7)), Is.True);
        }

        /// <summary>정산 응답으로 이미 신규 등록한 정령은 다음 조회에서 다시 세거나 N을 지우지 않는지 검사합니다.</summary>
        [Test]
        public void SyncCompleted_AfterRegisterCompleted_KeepsUnreadAndDoesNotRecount()
        {
            _recorder.SyncCompleted(new[] { 2 });
            _recorder.RegisterCompleted(7, alreadySeen: false);
            _changedCount = 0;

            Assert.That(_recorder.SyncCompleted(new[] { 2, 7 }), Is.Zero);

            Assert.That(_progress.IsUnread(CollectionIdOf(7)), Is.True);
            Assert.That(_progress.RestoredCount, Is.EqualTo(2));
            Assert.That(_changedCount, Is.Zero);
        }

        /// <summary>정령 목록이 null이면 거부하는지 검사합니다.</summary>
        [Test]
        public void SyncCompleted_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _recorder.SyncCompleted(null));
        }

        /// <summary>
        /// 회귀 검사: 마지막 페이즈 정산 응답을 잃고(등록 호출 없음) 홈으로 나간 뒤 작업대를 다시 조회하면,
        /// 백엔드에 남은 완료 정령이 도감에 신규로 등록되는지 모의 백엔드로 검사합니다.
        /// </summary>
        [Test]
        public void ReportResponseLostThenHome_WorkbenchSnapshotRegistersCompletedSpirit()
        {
            DateTime now = new DateTime(2026, 11, 25, 9, 0, 0, DateTimeKind.Utc);
            OwnedRelic lastPhase = new OwnedRelic(7, now - TimeSpan.FromDays(1), RelicStatus.InProgress, Phase.Guard, null, null, 0);
            OwnedRelic spirit = new OwnedRelic(2, now - TimeSpan.FromDays(9), RelicStatus.Completed, Phase.Guard,
                now - TimeSpan.FromDays(3), now - TimeSpan.FromDays(3), 0);
            var inventory = new RelicInventory(ClarityPolicy.Default, RelicInventory.DefaultWorkbenchCapacity, new[] { lastPhase, spirit });
            var planner = new PlayPlanner(ClarityPolicy.Default, RestorePolicy.Default, MinigameDifficulty.Normal);
            var backend = new MockWorkbenchBackend(inventory, planner, WorkbenchRelicSeed.Instance.Count, WorkbenchRelicSeed.Instance,
                new System.Random(0), () => now, 0f);

            // 시작 시 채우기
            _recorder.SyncCompleted(Run<WorkbenchSnapshot>((c, f) => backend.GetWorkbench(c, f)).Spirits.Select(s => s.RelicId));
            Assert.That(_progress.IsRestored(CollectionIdOf(7)), Is.False);

            // 마지막 페이즈를 성공으로 보고했지만 응답을 잃어 RegisterCompleted가 불리지 않았다
            PurificationSession session = Run<PurificationSession>((c, f) => backend.StartPurification(7, null, c, f));
            SettlementResult lost = Run<SettlementResult>((c, f) => backend.ReportPurification(session.SessionToken,
                new PlayReport(PlayResult.Success, null, 3, 1, 10, 0), c, f));
            Assert.That(lost.CompletedNow, Is.True, "준비 확인: 이 보고로 최초 정화가 끝나야 합니다.");
            Assert.That(_progress.IsRestored(CollectionIdOf(7)), Is.False);

            // 홈으로 나가 작업대를 다시 조회
            _recorder.SyncCompleted(Run<WorkbenchSnapshot>((c, f) => backend.GetWorkbench(c, f)).Spirits.Select(s => s.RelicId));

            Assert.That(_progress.IsRestored(CollectionIdOf(7)), Is.True);
            Assert.That(_progress.IsUnread(CollectionIdOf(7)), Is.True);
            Assert.That(_progress.IsUnread(CollectionIdOf(2)), Is.False);
        }

        /// <summary>지연 0인 모의 백엔드 코루틴을 끝까지 돌리고 성공 값을 돌려준다.</summary>
        private static T Run<T>(Func<Action<T>, Action<string>, IEnumerator> call)
        {
            T value = default;
            string error = null;
            Drain(call(v => value = v, e => error = e));
            Assert.That(error, Is.Null);
            return value;
        }

        private static void Drain(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator inner) Drain(inner);
            }
        }

        /// <summary>모든 relicId를 카탈로그에 없는 도감 ID로 돌려주는 유물 목록.</summary>
        private sealed class MissingCollectionCatalog : IWorkbenchRelicCatalog
        {
            public int Count => 1;

            public bool TryFind(int relicId, out WorkbenchRelicInfo info)
            {
                info = new WorkbenchRelicInfo(relicId, "not-in-catalog", "삼국", "별칭", "유물", "정령", SealMedium.Root);
                return true;
            }
        }
    }
}
