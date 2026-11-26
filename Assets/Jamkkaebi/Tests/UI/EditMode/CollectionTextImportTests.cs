using System.IO;
using System.Linq;
using System.Text;
using Jamkkaebi.Editor.TextTables;
using Jamkkaebi.Editor.TextTables.Collection;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.UI.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.UI.EditMode
{
    public sealed class CollectionTextImportTests
    {
        private const string ErasCsv =
            "id,시대 이름(참고),건물 이름(참고),기간 문구,시대 설명,비고\n" +
            "samguk,삼국,석굴암,기간1,설명1,\n" +
            "goryeo,고려,경천사지 십층석탑,기간2,설명2,\n" +
            "joseon,조선,경복궁,기간3,\"설명3, 쉼표\n둘째 줄\",메모\n";

        private CollectionCatalog _structure;
        private TextTableReport _report;

        [SetUp]
        public void SetUp()
        {
            _structure = CollectionCatalogData.CreateCatalog();
            _report = new TextTableReport();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_structure);

        private static string ReadRepositoryCsv(string path) => File.ReadAllText(Path.GetFullPath(path), Encoding.UTF8);

        private string RelicsCsv(System.Func<CollectionRelicDefinition, string> spiritName = null)
        {
            StringBuilder builder = new StringBuilder("id,유물 이름(참고),도깨비 기본 이름(참고),유물 설명,복원 기록\n");
            foreach (CollectionRelicDefinition relic in _structure.Relics)
            {
                builder.Append(relic.Id).Append(',').Append(relic.Name).Append(',')
                    .Append(spiritName != null ? spiritName(relic) : relic.SpiritName).Append(',')
                    .Append("설명 ").Append(relic.Id).Append(",기록 ").Append(relic.Id).Append('\n');
            }
            return builder.ToString();
        }

        /// <summary>저장소의 CSV 두 개가 오류·경고 없이 카탈로그의 모든 시대·유물 ID를 덮는지 검사합니다.</summary>
        [Test]
        public void RepositoryCsv_CoversEveryCatalogIdWithoutProblems()
        {
            CollectionTextImportResult result = CollectionTextImport.Build(_structure, null,
                ReadRepositoryCsv(CollectionTextImport.ErasCsvPath),
                ReadRepositoryCsv(CollectionTextImport.RelicsCsvPath), _report);

            Assert.That(result, Is.Not.Null, _report.Format());
            Assert.That(_report.Warnings, Is.Empty, _report.Format());
            Assert.That(result.Eras.Select(era => era.Id), Is.EqualTo(_structure.Eras.Select(era => era.Id)));
            Assert.That(result.Relics.Select(relic => relic.Id), Is.EqualTo(_structure.Relics.Select(relic => relic.Id)));
            Assert.That(result.Eras.All(era => era.Period.Length > 0 && era.Description.Length > 0));
            Assert.That(result.Relics.All(relic => relic.Description.Length > 0 && relic.ConservationNote.Length > 0));
        }

        /// <summary>CSV 텍스트는 반영하고, 이름·소속·순서·강조색은 구조 카탈로그 값을 유지하는지 검사합니다.</summary>
        [Test]
        public void Build_AppliesCsvTextAndKeepsStructure()
        {
            CollectionTextImportResult result = CollectionTextImport.Build(_structure, null, ErasCsv, RelicsCsv(), _report);

            Assert.That(result, Is.Not.Null, _report.Format());
            Assert.That(result.Eras[2].Description, Is.EqualTo("설명3, 쉼표\n둘째 줄"));
            Assert.That(result.Eras[0].Period, Is.EqualTo("기간1"));
            Assert.That(result.Eras[0].BuildingName, Is.EqualTo(_structure.Eras[0].BuildingName));
            Assert.That(result.Relics[4].Description, Is.EqualTo("설명 " + _structure.Relics[4].Id));
            Assert.That(result.Relics[4].ConservationNote, Is.EqualTo("기록 " + _structure.Relics[4].Id));
            Assert.That(result.Relics[4].EraId, Is.EqualTo(_structure.Relics[4].EraId));
            Assert.That(result.Relics[4].Accent, Is.EqualTo(_structure.Relics[4].Accent));
            Assert.That(result.ChangedEraCount, Is.EqualTo(3));
            Assert.That(result.ChangedRelicCount, Is.EqualTo(9));
        }

        /// <summary>참고 열이 서버 기준 이름과 다르면 경고만 하고 이름은 덮어쓰지 않는지 검사합니다.</summary>
        [Test]
        public void Build_ReferenceMismatch_WarnsWithoutOverwriting()
        {
            string firstId = _structure.Relics[0].Id;
            CollectionTextImportResult result = CollectionTextImport.Build(_structure, null, ErasCsv,
                RelicsCsv(relic => relic.Id == firstId ? "새 이름" : relic.SpiritName), _report);

            Assert.That(result, Is.Not.Null, _report.Format());
            Assert.That(_report.Warnings.Single(), Does.Contain(firstId).And.Contain("새 이름"));
            Assert.That(result.Relics[0].SpiritName, Is.EqualTo(_structure.Relics[0].SpiritName));
        }

        /// <summary>한 표라도 오류가 있으면 결과를 만들지 않는지 검사합니다.</summary>
        [Test]
        public void Build_ErrorInEitherTable_ReturnsNull()
        {
            string erasWithoutJoseon = string.Join("\n", ErasCsv.Split('\n').Take(3));

            Assert.That(CollectionTextImport.Build(_structure, null, erasWithoutJoseon, RelicsCsv(), _report), Is.Null);
            Assert.That(_report.Errors.Single(), Does.Contain("joseon"));
        }

        /// <summary>이전 카탈로그와 텍스트가 같은 행은 바뀐 행으로 세지 않는지 검사합니다.</summary>
        [Test]
        public void Build_UnchangedRows_AreNotCounted()
        {
            CollectionTextImportResult first = CollectionTextImport.Build(_structure, null, ErasCsv, RelicsCsv(), _report);
            CollectionCatalog previous = ScriptableObject.CreateInstance<CollectionCatalog>();
            try
            {
                previous.Configure(first.Eras, first.Relics);
                string changedEras = ErasCsv.Replace("기간1", "바뀐 기간");

                CollectionTextImportResult second = CollectionTextImport.Build(_structure, previous, changedEras,
                    RelicsCsv(), _report);

                Assert.That(second.ChangedEraCount, Is.EqualTo(1));
                Assert.That(second.ChangedRelicCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(previous);
            }
        }
    }
}
