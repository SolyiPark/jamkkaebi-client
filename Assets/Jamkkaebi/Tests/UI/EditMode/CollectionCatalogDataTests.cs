using System.IO;
using System.Linq;
using System.Text;
using Jamkkaebi.Editor.TextTables;
using Jamkkaebi.Editor.TextTables.Collection;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.UI.Collection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Jamkkaebi.Tests.UI.EditMode
{
    public sealed class CollectionCatalogDataTests
    {
        private CollectionCatalog _catalog;

        /// <summary>각 검사에 서버 시드 기준 구조의 새 메모리 카탈로그를 제공합니다.</summary>
        [SetUp]
        public void SetUp() => _catalog = CollectionCatalogData.CreateCatalog();

        /// <summary>검사에 사용한 메모리 카탈로그를 파괴합니다.</summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_catalog);

        /// <summary>시대와 유물의 ID·소속·순서·명칭이 서버 시드(relicId 1~9 순서)와 일치하는지 검사합니다.</summary>
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
                    "samguk-turtle-water-dropper:samguk:거북이 모양 연적",
                    "samguk-incense-burner:samguk:금동대향로",
                    "samguk-frog-figurine:samguk:개구리 모양 토우",
                    "goryeo-pomegranate-water-dropper:goryeo:청자 석류 모양 연적",
                    "goryeo-celadon:goryeo:상감 청자 매병",
                    "goryeo-lacquerware:goryeo:나전칠기",
                    "joseon-phoenix-hairpin:joseon:봉 뒤꽂이",
                    "joseon-horse-medallion:joseon:마패",
                    "joseon-cat-sparrow-painting:joseon:묘작도"
                }));
        }

        /// <summary>도깨비 기본 이름과 시대별 건물 이름이 서버 시드와 일치하는지 검사합니다.</summary>
        [Test]
        public void SpiritAndBuildingNamesMatchServerSeed()
        {
            Assert.That(_catalog.Relics.Select(relic => relic.SpiritName), Is.EqualTo(new[]
            {
                "거북이", "향산이", "개굴이", "석류동이", "구름결", "밤바다", "봉이", "말발굽", "묘작이"
            }));
            Assert.That(_catalog.Eras.Select(era => era.BuildingName), Is.EqualTo(new[]
            {
                "석굴암", "경천사지 십층석탑", "경복궁"
            }));
        }

        /// <summary>
        /// 도감 씬에 사용하는 저장 자산이 구조 코드와 저장소 CSV를 합친 결과와 같은지 검사합니다.
        /// 실패하면 CSV나 CollectionCatalogData를 고친 뒤 Jamkkaebi/Collection/Import Text CSV를 실행하지 않은 것입니다.
        /// </summary>
        [Test]
        public void SceneCatalogAssetMatchesStructureAndCsv()
        {
            CollectionCatalog saved = AssetDatabase.LoadAssetAtPath<CollectionCatalog>(
                "Assets/Jamkkaebi/Configuration/CollectionCatalog.asset");
            Assert.That(saved, Is.Not.Null);
            TextTableReport report = new TextTableReport();
            CollectionTextImportResult expected = CollectionTextImport.Build(_catalog, null,
                File.ReadAllText(Path.GetFullPath(CollectionTextImport.ErasCsvPath), Encoding.UTF8),
                File.ReadAllText(Path.GetFullPath(CollectionTextImport.RelicsCsvPath), Encoding.UTF8), report);
            Assert.That(expected, Is.Not.Null, report.Format());

            Assert.That(saved.Eras.Count, Is.EqualTo(expected.Eras.Count));
            Assert.That(saved.Relics.Count, Is.EqualTo(expected.Relics.Count));
            for (int i = 0; i < expected.Eras.Count; i++)
            {
                Assert.That(saved.Eras[i].Id, Is.EqualTo(expected.Eras[i].Id));
                Assert.That(saved.Eras[i].Name, Is.EqualTo(expected.Eras[i].Name));
                Assert.That(saved.Eras[i].Period, Is.EqualTo(expected.Eras[i].Period));
                Assert.That(saved.Eras[i].Description, Is.EqualTo(expected.Eras[i].Description));
                Assert.That(saved.Eras[i].BuildingName, Is.EqualTo(expected.Eras[i].BuildingName));
            }
            for (int i = 0; i < expected.Relics.Count; i++)
            {
                CollectionRelicDefinition actual = saved.Relics[i];
                CollectionRelicDefinition wanted = expected.Relics[i];
                Assert.That(actual.Id, Is.EqualTo(wanted.Id));
                Assert.That(actual.EraId, Is.EqualTo(wanted.EraId));
                Assert.That(actual.Name, Is.EqualTo(wanted.Name));
                Assert.That(actual.SpiritName, Is.EqualTo(wanted.SpiritName));
                Assert.That(actual.Description, Is.EqualTo(wanted.Description));
                Assert.That(actual.ConservationNote, Is.EqualTo(wanted.ConservationNote));
            }
        }
    }
}
