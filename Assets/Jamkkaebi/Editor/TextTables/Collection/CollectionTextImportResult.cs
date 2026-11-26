using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Collection;

namespace Jamkkaebi.Editor.TextTables.Collection
{
    /// <summary>도감 텍스트 CSV를 구조·이전 그림과 합친 결과입니다. 카탈로그의 Configure에 그대로 넘깁니다.</summary>
    public sealed class CollectionTextImportResult
    {
        /// <summary>구조 카탈로그 순서의 시대 정의입니다.</summary>
        public IReadOnlyList<CollectionEraDefinition> Eras { get; }

        /// <summary>구조 카탈로그 순서의 유물 정의입니다.</summary>
        public IReadOnlyList<CollectionRelicDefinition> Relics { get; }

        /// <summary>이전 자산과 비교해 그림 외의 값이 바뀌었거나 새로 생긴 시대 수입니다.</summary>
        public int ChangedEraCount { get; }

        /// <summary>이전 자산과 비교해 그림 외의 값이 바뀌었거나 새로 생긴 유물 수입니다.</summary>
        public int ChangedRelicCount { get; }

        internal CollectionTextImportResult(IReadOnlyList<CollectionEraDefinition> eras,
            IReadOnlyList<CollectionRelicDefinition> relics, int changedEraCount, int changedRelicCount)
        {
            Eras = eras;
            Relics = relics;
            ChangedEraCount = changedEraCount;
            ChangedRelicCount = changedRelicCount;
        }
    }
}
