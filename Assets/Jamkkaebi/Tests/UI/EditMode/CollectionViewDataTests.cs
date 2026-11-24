using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.UI.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.UI.EditMode
{
    public sealed class CollectionViewDataTests
    {
        private CollectionCatalog _catalog;

        [SetUp]
        public void SetUp() => _catalog = CollectionCatalogData.CreateCatalog();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_catalog);

        /// <summary>
        /// 유물 5종(삼국 1·고려 3·조선 1)을 복원해 고려 건물만 해금되고, 고려 2종은 확인한 상태의 기록.
        /// 시대마다 복원 수·건물 해금·신규 여부가 서로 다르게 섞인 화면 검사용이다.
        /// </summary>
        private CollectionProgress FiveRestoredProgress()
        {
            var progress = new CollectionProgress(_catalog);
            progress.RegisterRestorationCompleted("samguk-frog-figurine");
            progress.RegisterRestorationCompleted("goryeo-celadon");
            progress.RegisterRestorationCompleted("goryeo-pomegranate-water-dropper");
            progress.RegisterRestorationCompleted("goryeo-lacquerware");
            progress.RegisterRestorationCompleted("joseon-phoenix-hairpin");
            progress.MarkViewed("goryeo-pomegranate-water-dropper");
            progress.MarkViewed("goryeo-lacquerware");
            return progress;
        }

        /// <summary>원본 장부가 바뀌어도 이미 화면에 준 안내지가 같이 바뀌지 않는지 검사합니다.</summary>
        [Test]
        public void ProgressChangesDoNotMutatePreviouslyRenderedData()
        {
            var progress = new CollectionProgress(_catalog);
            progress.RegisterRestorationCompleted("samguk-frog-figurine");
            var before = new CollectionViewData(progress, null, CollectionPage.Era, "samguk", null);

            progress.MarkViewed("samguk-frog-figurine");
            progress.RegisterRestorationCompleted("samguk-turtle-water-dropper");
            progress.RegisterRestorationCompleted("samguk-incense-burner");

            Assert.That(before.RestoredCount, Is.EqualTo(1));
            Assert.That(before.IsUnread("samguk-frog-figurine"), Is.True);
            Assert.That(before.IsRestored("samguk-turtle-water-dropper"), Is.False);
            Assert.That(before.RestoredInEra("samguk"), Is.EqualTo(1));
            Assert.That(before.IsBuildingUnlocked("samguk"), Is.False);

            var after = new CollectionViewData(progress, null, CollectionPage.Era, "samguk", null);
            Assert.That(after.IsUnread("samguk-frog-figurine"), Is.False);
            Assert.That(after.RestoredInEra("samguk"), Is.EqualTo(3));
            Assert.That(after.IsBuildingUnlocked("samguk"), Is.True);
        }

        /// <summary>표시 데이터를 만드는 것만으로 신규 N을 지우거나 해금 규칙을 달리 계산하지 않는지 검사합니다.</summary>
        [Test]
        public void SnapshotPreservesDomainDecisionsWithoutAcknowledgingUnreadRecords()
        {
            var progress = FiveRestoredProgress();
            var data = new CollectionViewData(progress, null, CollectionPage.Relic, "samguk", "samguk-frog-figurine");

            Assert.That(data.Catalog, Is.SameAs(_catalog));
            Assert.That(data.SelectedRelicId, Is.EqualTo("samguk-frog-figurine"));
            Assert.That(data.RestoredCount, Is.EqualTo(5));
            Assert.That(data.TotalCount, Is.EqualTo(9));
            Assert.That(data.CompletedBuildingCount, Is.EqualTo(1));
            Assert.That(data.IsBuildingUnlocked("goryeo"), Is.True);
            Assert.That(data.IsBuildingUnlocked("samguk"), Is.False);
            Assert.That(data.HasUnreadInEra("samguk"), Is.True);
            Assert.That(progress.IsUnread("samguk-frog-figurine"), Is.True);
            Assert.That(data.IsRestored(null), Is.False);
            Assert.That(data.TotalInEra("unknown"), Is.Zero);
        }
    }
}
