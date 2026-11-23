using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.UI.Workbench;
using NUnit.Framework;
using UnityEditor;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>
    /// 작업대 유물 목록(서버 시드, relicId)과 도감 카탈로그(종류 ID)가 같은 유물을 가리키는지 검사합니다.
    /// 작업대는 최초 정화를 마친 유물을 CollectionId로 도감에 등록하므로, 둘이 어긋나면 다른 유물이 등록됩니다.
    /// </summary>
    public sealed class WorkbenchRelicSeedTests
    {
        private const string CatalogPath = "Assets/Jamkkaebi/Configuration/CollectionCatalog.asset";

        private CollectionCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath + "를 찾지 못했습니다.");
        }

        private static IEnumerable<WorkbenchRelicInfo> SeedRelics()
        {
            WorkbenchRelicSeed seed = WorkbenchRelicSeed.Instance;
            for (int relicId = 1; relicId <= seed.Count; relicId++)
            {
                Assert.That(seed.TryFind(relicId, out WorkbenchRelicInfo info), Is.True, $"relicId {relicId}가 없습니다.");
                yield return info;
            }
        }

        /// <summary>작업대 유물마다 도감 ID가 카탈로그에 있고, 시대·유물 이름·도깨비 이름이 같은지 검사합니다.</summary>
        [Test]
        public void EverySeedRelicMatchesCatalogEntry()
        {
            foreach (WorkbenchRelicInfo info in SeedRelics())
            {
                CollectionRelicDefinition relic = _catalog.FindRelic(info.CollectionId);
                Assert.That(relic, Is.Not.Null, $"relicId {info.RelicId}의 도감 ID '{info.CollectionId}'가 카탈로그에 없습니다.");
                Assert.That(relic.Name, Is.EqualTo(info.RealName), $"relicId {info.RelicId} 이름");
                Assert.That(relic.SpiritName, Is.EqualTo(info.DefaultSpiritName), $"relicId {info.RelicId} 도깨비 이름");
                Assert.That(_catalog.FindEra(relic.EraId).Name, Is.EqualTo(info.EraName), $"relicId {info.RelicId} 시대");
            }
        }

        /// <summary>작업대와 도감의 유물 수가 같고, 도감 ID가 겹치지 않아 모든 도감 유물이 한 번씩 대응되는지 검사합니다.</summary>
        [Test]
        public void SeedCoversEveryCatalogRelicOnce()
        {
            List<string> ids = SeedRelics().Select(info => info.CollectionId).ToList();

            Assert.That(ids, Is.Unique);
            Assert.That(ids, Is.EquivalentTo(_catalog.Relics.Select(relic => relic.Id)));
        }

        /// <summary>도감 순서가 relicId 순서와 같은지 검사합니다(카탈로그는 서버 시드 순서로 둔다).</summary>
        [Test]
        public void CatalogOrderFollowsRelicId()
        {
            Assert.That(_catalog.Relics.Select(relic => relic.Id),
                Is.EqualTo(SeedRelics().Select(info => info.CollectionId)));
        }

        /// <summary>도감 ID가 비어 있으면 거부하는지 검사합니다.</summary>
        [Test]
        public void Info_EmptyCollectionId_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new WorkbenchRelicInfo(1, "", "삼국", "별칭", "유물", "정령",
                Jamkkaebi.Scripts.Gameplay.Data.SealMedium.Root));
            Assert.Throws<System.ArgumentException>(() => new WorkbenchRelicInfo(1, null, "삼국", "별칭", "유물", "정령",
                Jamkkaebi.Scripts.Gameplay.Data.SealMedium.Root));
        }
    }
}
