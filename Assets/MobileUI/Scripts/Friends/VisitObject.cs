using UnityEngine;

namespace MobilePrototype.Friends
{
    /// <summary>방문 장면에서만 사용하는 현재 셀. 저장 앵커는 변경하지 않습니다.</summary>
    public sealed class VisitObject
    {
        public SnapshotPlacement Placement { get; }
        public Vector2Int Cell { get; private set; }
        public bool Unknown { get; }
        /// <summary>Starts the visit object at its saved anchor and marks placeholders or IDs greater than three as unknown.</summary>
        public VisitObject(SnapshotPlacement placement)
        { Placement = placement; Cell = placement.Anchor; Unknown = placement.Placeholder || placement.Id > 3; }
        /// <summary>Changes only the visit cell; the saved placement anchor is retained and the destination is not validated here.</summary>
        internal void MoveTo(Vector2Int cell) { Cell = cell; }
    }
}
