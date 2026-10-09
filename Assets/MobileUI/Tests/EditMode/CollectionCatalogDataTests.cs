using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Collection;
using MobilePrototype.Collection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MobilePrototype.Tests.EditMode
{
    public sealed class CollectionCatalogDataTests
    {
        private CollectionCatalog _catalog;

        /// <summary>각 검사에 확정 시트 정의의 새 메모리 카탈로그를 제공합니다.</summary>
        [SetUp]
        public void SetUp() => _catalog = CollectionCatalogData.CreateCatalog();

        /// <summary>검사에 사용한 메모리 카탈로그를 파괴합니다.</summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_catalog);

        /// <summary>시대와 유물의 ID·소속·명칭·순서가 확정 시트의 9개 행과 일치하는지 검사합니다.</summary>
        [Test]
        public void CatalogMatchesConfirmedSheetRowsInOrder()
        {
            Assert.That(_catalog.Eras.Select(era => era.Id + ":" + era.Name), Is.EqualTo(new[]
            {
                "samguk:삼국", "goryeo:고려", "joseon:조선"
            }));
            Assert.That(_catalog.Relics.Select(relic => relic.Id + ":" + relic.EraId + ":" + relic.Name),
                Is.EqualTo(new[]
                {
                    "samguk-standing-buddha:samguk:금동여래입상",
                    "samguk-incense-burner:samguk:금동대향로",
                    "samguk-crown:samguk:금관",
                    "goryeo-stone-buddha:goryeo:석불",
                    "goryeo-celadon:goryeo:고려청자",
                    "goryeo-lacquerware:goryeo:나전칠기",
                    "joseon-royal-seal:joseon:옥새",
                    "joseon-horse-medallion:joseon:마패",
                    "joseon-cat-sparrow-painting:joseon:묘작도"
                }));
        }

        /// <summary>도감 씬에 사용하는 저장 자산의 순서·표시명·미정 문구가 확정 정의 코드와 일치하는지 검사합니다.</summary>
        [Test]
        public void SceneCatalogAssetMatchesConfirmedDefinitions()
        {
            CollectionCatalog saved = AssetDatabase.LoadAssetAtPath<CollectionCatalog>(
                "Assets/MobileUI/Configuration/CollectionCatalog.asset");
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.Eras.Count, Is.EqualTo(_catalog.Eras.Count));
            Assert.That(saved.Relics.Count, Is.EqualTo(_catalog.Relics.Count));
            for (int i = 0; i < _catalog.Eras.Count; i++)
            {
                Assert.That(saved.Eras[i].Id, Is.EqualTo(_catalog.Eras[i].Id));
                Assert.That(saved.Eras[i].Name, Is.EqualTo(_catalog.Eras[i].Name));
            }
            for (int i = 0; i < _catalog.Relics.Count; i++)
            {
                CollectionRelicDefinition actual = saved.Relics[i];
                CollectionRelicDefinition expected = _catalog.Relics[i];
                Assert.That(actual.Id, Is.EqualTo(expected.Id));
                Assert.That(actual.EraId, Is.EqualTo(expected.EraId));
                Assert.That(actual.Name, Is.EqualTo(expected.Name));
                Assert.That(actual.SpiritName, Is.EqualTo(expected.SpiritName));
                Assert.That(actual.Description, Is.EqualTo(expected.Description));
                Assert.That(actual.ConservationNote, Is.EqualTo(expected.ConservationNote));
            }
        }

        /// <summary>더미 기록이 확정 유물 5종과 고려 건물 하나만 완료하며 신규 확인 정책을 따르는지 검사합니다.</summary>
        [Test]
        public void DemoProgressUsesConfirmedRelicsAndKeepsCompletionRules()
        {
            CollectionProgress progress = CollectionDemoData.CreateProgress(_catalog);
            Assert.That(progress.TotalCount, Is.EqualTo(9));
            Assert.That(progress.RestoredCount, Is.EqualTo(5));
            Assert.That(progress.CompletedBuildingCount, Is.EqualTo(1));
            Assert.That(progress.IsBuildingUnlocked("goryeo"), Is.True);
            Assert.That(progress.IsBuildingUnlocked("samguk"), Is.False);
            Assert.That(progress.IsBuildingUnlocked("joseon"), Is.False);
            Assert.That(progress.IsRestored("samguk-crown"), Is.True);
            Assert.That(progress.IsRestored("joseon-royal-seal"), Is.True);
            Assert.That(progress.IsUnread("goryeo-stone-buddha"), Is.False);
            Assert.That(progress.IsUnread("goryeo-lacquerware"), Is.False);
            Assert.That(progress.IsUnread("goryeo-celadon"), Is.True);
            Assert.That(progress.IsUnread("samguk-crown"), Is.True);
            Assert.That(progress.IsUnread("joseon-royal-seal"), Is.True);
        }
    }
}
