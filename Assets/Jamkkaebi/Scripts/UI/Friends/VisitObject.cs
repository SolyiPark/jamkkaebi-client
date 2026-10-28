using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Friends
{
    /// <summary>방문 장면에서만 사용하는 현재 셀. 저장 앵커는 변경하지 않습니다.</summary>
    public sealed class VisitObject
    {
        public SnapshotPlacement Placement { get; }
        public Vector2Int Cell { get; private set; }
        public bool Unknown { get; }
        /// <summary>원본 배치를 보관하고 방문 중 이동에 사용할 별도 셀 상태를 초기화합니다.</summary>
        public VisitObject(SnapshotPlacement placement)
        { Placement = placement; Cell = placement.Anchor; Unknown = placement.Placeholder || placement.Id > 3; }
        /// <summary>방문 중 임시 셀 위치를 바꾸며 원본 스냅샷 배치는 변경하지 않습니다.</summary>
        internal void MoveTo(Vector2Int cell) { Cell = cell; }
    }
}
