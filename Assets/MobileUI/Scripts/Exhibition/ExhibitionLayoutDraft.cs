using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace MobilePrototype.Exhibition
{
    /// <summary>관리 대상 전체. Placed=false도 포함해야 회수한 객체가 서버에서 제거된다.</summary>
    public sealed class ExhibitionLayoutDraft
    {
        public Vector2Int GridSize { get; }
        public ReadOnlyCollection<ExhibitionObjectData> Objects { get; }
        /// <summary>격자 규격과 관리 대상 전체를 복사해 송신 중 변경되지 않는 초안을 생성합니다.</summary>
        public ExhibitionLayoutDraft(Vector2Int gridSize, IEnumerable<ExhibitionObjectData> objects)
        {
            GridSize = gridSize;
            Objects = new List<ExhibitionObjectData>(objects ?? throw new ArgumentNullException(nameof(objects))).AsReadOnly();
        }
    }
}
