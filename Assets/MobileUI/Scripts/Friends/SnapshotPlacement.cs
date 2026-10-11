using UnityEngine;

namespace MobilePrototype.Friends
{
    /// <summary>스냅샷의 저장 배치. 런타임 이동 상태와 분리한 읽기 전용 값입니다.</summary>
    public sealed class SnapshotPlacement
    {
        public bool IsBuilding { get; }
        public int Id { get; }
        public string Name { get; }
        public string Stage { get; }
        public int Level { get; }
        public Vector2Int Anchor { get; }
        public string Key => (IsBuilding ? "BUILDING:" : "SPIRIT:") + Id;
        public bool Placeholder { get; }
        public SnapshotPlacement(bool building, int id, string name, Vector2Int anchor, string stage = "AWAKENED", int level = 1, bool placeholder = false)
        { IsBuilding = building; Id = id; Name = name; Anchor = anchor; Stage = stage; Level = level; Placeholder = placeholder; }
    }
}
