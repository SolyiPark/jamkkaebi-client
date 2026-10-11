using UnityEngine;

namespace MobilePrototype.Friends
{
    /// <summary>방문 장면에서만 사용하는 현재 셀. 저장 앵커는 변경하지 않습니다.</summary>
    public sealed class VisitObject
    {
        public SnapshotPlacement Placement { get; }
        public Vector2Int Cell { get; private set; }
        public bool Unknown { get; }
        public VisitObject(SnapshotPlacement placement)
        { Placement = placement; Cell = placement.Anchor; Unknown = placement.Placeholder || placement.Id > 3; }
        internal void MoveTo(Vector2Int cell) { Cell = cell; }
    }
}
