using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Collection;

namespace Jamkkaebi.Editor.TextTables.Collection
{
    /// <summary>
    /// 도감 텍스트 CSV(시대·유물)를 구조 카탈로그와 합칩니다. 에디터 임포트 전용입니다.
    /// ID·소속·순서·강조색·이름은 구조 카탈로그(서버 시드 기준)를 쓰고, CSV는 설명류 텍스트만 바꿉니다.
    /// CSV의 참고 열(이름)이 구조와 다르면 경고만 하고 덮어쓰지 않습니다. 그림은 같은 ID의 이전 정의에서 옮깁니다.
    /// </summary>
    public static class CollectionTextImport
    {
        public const string ErasCsvPath = "Assets/Jamkkaebi/Configuration/Text/collection_eras.csv";
        public const string RelicsCsvPath = "Assets/Jamkkaebi/Configuration/Text/collection_relics.csv";

        public const string IdColumn = "id";
        public const string NoteColumn = "비고";

        public const string EraNameColumn = "시대 이름(참고)";
        public const string BuildingNameColumn = "건물 이름(참고)";
        public const string PeriodColumn = "기간 문구";
        public const string EraDescriptionColumn = "시대 설명";

        public const string RelicEraColumn = "시대(참고)";
        public const string RelicNameColumn = "유물 이름(참고)";
        public const string SpiritNameColumn = "도깨비 기본 이름(참고)";
        public const string RelicDescriptionColumn = "유물 설명";
        public const string ConservationNoteColumn = "복원 기록";

        /// <summary>collection_eras.csv의 열 구성입니다.</summary>
        public static TextTableSchema EraSchema { get; } = new TextTableSchema("collection_eras.csv", IdColumn,
            new[]
            {
                TextTableColumn.Reference(EraNameColumn),
                TextTableColumn.Reference(BuildingNameColumn),
                TextTableColumn.Value(PeriodColumn),
                TextTableColumn.Value(EraDescriptionColumn)
            },
            new[] { NoteColumn });

        /// <summary>collection_relics.csv의 열 구성입니다.</summary>
        public static TextTableSchema RelicSchema { get; } = new TextTableSchema("collection_relics.csv", IdColumn,
            new[]
            {
                TextTableColumn.Reference(RelicEraColumn),
                TextTableColumn.Reference(RelicNameColumn),
                TextTableColumn.Reference(SpiritNameColumn),
                TextTableColumn.Value(RelicDescriptionColumn),
                TextTableColumn.Value(ConservationNoteColumn)
            },
            new[] { NoteColumn });

        /// <summary>
        /// 두 CSV를 검증해 구조 카탈로그 순서의 새 정의를 만듭니다. 오류가 있으면 null을 반환하고 report에 이유를 남깁니다.
        /// </summary>
        /// <param name="structure">ID·순서·이름의 기준이 되는 카탈로그입니다(CollectionCatalogData.CreateCatalog).</param>
        /// <param name="previous">그림을 옮기고 변경 행을 셀 이전 카탈로그입니다. 처음 만들 때는 null입니다.</param>
        /// <param name="erasCsv">collection_eras.csv 내용입니다.</param>
        /// <param name="relicsCsv">collection_relics.csv 내용입니다.</param>
        /// <param name="report">오류·경고를 추가할 보고서입니다.</param>
        /// <exception cref="ArgumentNullException">previous를 뺀 인자 중 하나가 null인 경우입니다.</exception>
        public static CollectionTextImportResult Build(CollectionCatalog structure, CollectionCatalog previous,
            string erasCsv, string relicsCsv, TextTableReport report)
        {
            if (structure == null)
                throw new ArgumentNullException(nameof(structure));
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            TextTable eraTable = TextTable.Read(erasCsv, EraSchema, structure.Eras.Select(era => era.Id), report);
            TextTable relicTable = TextTable.Read(relicsCsv, RelicSchema, structure.Relics.Select(relic => relic.Id), report);
            if (eraTable == null || relicTable == null)
                return null;

            List<CollectionEraDefinition> eras = new List<CollectionEraDefinition>();
            int changedEras = 0;
            foreach (CollectionEraDefinition era in structure.Eras)
            {
                TextTableRow row = eraTable.Rows[era.Id];
                WarnIfReferenceDiffers(EraSchema, row, EraNameColumn, era.Name, report);
                WarnIfReferenceDiffers(EraSchema, row, BuildingNameColumn, era.BuildingName, report);

                CollectionEraDefinition old = previous != null ? previous.FindEra(era.Id) : null;
                CollectionEraDefinition next = new CollectionEraDefinition(era.Id, era.Name,
                    row.Get(PeriodColumn), row.Get(EraDescriptionColumn), era.BuildingName, era.Accent,
                    old?.OverviewIllustration, old?.ContextIllustration, old?.BuildingIllustration,
                    old?.LockedBuildingIllustration, old?.BackgroundIllustration);
                if (old == null || !SameText(old, next))
                    changedEras++;
                eras.Add(next);
            }

            List<CollectionRelicDefinition> relics = new List<CollectionRelicDefinition>();
            int changedRelics = 0;
            foreach (CollectionRelicDefinition relic in structure.Relics)
            {
                TextTableRow row = relicTable.Rows[relic.Id];
                WarnIfReferenceDiffers(RelicSchema, row, RelicEraColumn, structure.FindEra(relic.EraId)?.Name, report);
                WarnIfReferenceDiffers(RelicSchema, row, RelicNameColumn, relic.Name, report);
                WarnIfReferenceDiffers(RelicSchema, row, SpiritNameColumn, relic.SpiritName, report);

                CollectionRelicDefinition old = previous != null ? previous.FindRelic(relic.Id) : null;
                CollectionRelicDefinition next = new CollectionRelicDefinition(relic.Id, relic.EraId, relic.Name,
                    relic.SpiritName, row.Get(RelicDescriptionColumn), row.Get(ConservationNoteColumn), relic.Accent,
                    old?.Illustration, old?.SpiritIllustration, old?.Thumbnail, old?.LockedIllustration,
                    old?.BackgroundIllustration);
                if (old == null || !SameText(old, next))
                    changedRelics++;
                relics.Add(next);
            }

            return new CollectionTextImportResult(eras, relics, changedEras, changedRelics);
        }

        /// <summary>참고 열에 값이 있고 기준 값과 다르면 경고합니다. 빈 참고 셀과 빠진 참고 열은 검사하지 않습니다.</summary>
        private static void WarnIfReferenceDiffers(TextTableSchema schema, TextTableRow row, string column,
            string authoritative, TextTableReport report)
        {
            if (!row.TryGet(column, out string value) || value.Length == 0)
                return;
            if (!string.Equals(value, authoritative, StringComparison.Ordinal))
                report.AddWarning($"{schema.TableName} {row.SheetRow}행 ({row.Key}): '{column}'이(가) '{value}'이지만 " +
                    $"서버 기준 값 '{authoritative}'을(를) 유지합니다.");
        }

        /// <summary>그림을 뺀 시대 값이 같은지 비교합니다. 강조색도 구조 값이므로 함께 봅니다.</summary>
        private static bool SameText(CollectionEraDefinition a, CollectionEraDefinition b) =>
            a.Name == b.Name && a.Period == b.Period && a.Description == b.Description &&
            a.BuildingName == b.BuildingName && a.Accent == b.Accent;

        /// <summary>그림을 뺀 유물 값이 같은지 비교합니다.</summary>
        private static bool SameText(CollectionRelicDefinition a, CollectionRelicDefinition b) =>
            a.EraId == b.EraId && a.Name == b.Name && a.SpiritName == b.SpiritName &&
            a.Description == b.Description && a.ConservationNote == b.ConservationNote && a.Accent == b.Accent;
    }
}
