using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.Gameplay.Data;
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
            Assert.Throws<System.ArgumentNullException>(() => new CollectionRecorder(null, WorkbenchRelicSeed.Instance));
            Assert.Throws<System.ArgumentNullException>(() => new CollectionRecorder(_progress, null));
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
