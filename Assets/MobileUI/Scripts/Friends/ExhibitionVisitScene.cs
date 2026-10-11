using System;
using System.Collections.Generic;
using MobilePrototype.Exhibition;
using UnityEngine;

namespace MobilePrototype.Friends
{
    /// <summary>패키지 검증과 방문 전용 재현 상태. 홈의 배치·정령 데이터에 접근하지 않습니다.</summary>
    public sealed class ExhibitionVisitScene
    {
        private readonly List<VisitObject> _objects = new List<VisitObject>();
        private readonly List<string> _warnings = new List<string>();
        public ExhibitionSnapshot Snapshot { get; }
        public IReadOnlyList<VisitObject> Objects => _objects.AsReadOnly();
        public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();
        /// <summary>Builds visit objects, skipping invalid or duplicate identities with warnings and retaining unknown objects as placeholders.</summary>
        /// <exception cref="ArgumentException">The snapshot or owner is missing, placements lack a grid, or the supplied grid is not 7×7.</exception>
        public ExhibitionVisitScene(ExhibitionSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.OwnerCode)) throw new ArgumentException("전시 정보가 올바르지 않아요.");
            Snapshot = snapshot;
            if (!snapshot.HasGrid)
            {
                if (snapshot.Placements.Count != 0) throw new ArgumentException("전시 배치 정보를 읽을 수 없어요.");
                return;
            }
            if (snapshot.Width != 7 || snapshot.Height != 7) throw new ArgumentException($"서버 전시는 {snapshot.Width}×{snapshot.Height}입니다. 현재 화면은 7×7 전시만 지원해요.");
            var seen = new HashSet<string>();
            foreach (var row in snapshot.Placements)
            {
                if (row == null || row.Id < 1 || !InBounds(row.Anchor)) { _warnings.Add("잘못된 배치는 표시하지 않았어요."); continue; }
                if (!seen.Add(row.Key)) { _warnings.Add("중복된 배치는 표시하지 않았어요."); continue; }
                var obj = new VisitObject(row);
                if (obj.Unknown) _warnings.Add("아직 지원하지 않는 객체는 임시 모습으로 표시해요.");
                _objects.Add(obj);
            }
        }
        /// <summary>Returns whether both logical cell coordinates lie in the inclusive range zero through six.</summary>
        private static bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < 7 && cell.y < 7;
        /// <summary>Moves each NPC at most one adjacent cell, avoiding occupied cells and grid boundaries while preserving saved anchors.</summary>
        /// <param name="step">Nonnegative step counter used to vary the preferred direction.</param>
        public void Walk(int step)
        {
            var occupied = new HashSet<Vector2Int>();
            foreach (var obj in _objects) occupied.Add(obj.Cell);
            Vector2Int[] directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
            int index = 0;
            foreach (var obj in _objects)
            {
                if (obj.Placement.IsBuilding) continue;
                occupied.Remove(obj.Cell);
                for (int offset = 0; offset < 4; offset++)
                {
                    var target = obj.Cell + directions[(step + index + offset) % 4];
                    if (!InBounds(target) || occupied.Contains(target)) continue;
                    obj.MoveTo(target); break;
                }
                occupied.Add(obj.Cell); index++;
            }
        }
        /// <summary>Returns the visit cell center in platform-local coordinates using the supplied grid.</summary>
        public Vector2 LocalPosition(VisitObject obj, ExhibitionGrid grid) => grid.CellCenter(obj.Cell);
    }
}
