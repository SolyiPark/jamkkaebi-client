using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.UI.Exhibition;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Friends
{
    /// <summary>패키지 검증과 방문 전용 재현 상태. 홈의 배치·정령 데이터에 접근하지 않습니다.</summary>
    public sealed class ExhibitionVisitScene
    {
        private readonly List<VisitObject> _objects = new List<VisitObject>();
        private readonly List<string> _warnings = new List<string>();
        public ExhibitionSnapshot Snapshot { get; }
        public IReadOnlyList<VisitObject> Objects => _objects.AsReadOnly();
        public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();
        /// <summary>스냅샷 규격과 배치를 검증하고 원본과 분리된 방문 객체 상태를 생성합니다.</summary>
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
        /// <summary>논리 셀이 방문 화면의 7×7 범위 안에 있는지 확인합니다.</summary>
        private static bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < 7 && cell.y < 7;
        /// <summary>건물과 다른 NPC를 피하는 임시 이동을 진행하며 저장된 배치 앵커는 유지합니다.</summary>
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
        /// <summary>방문 객체의 현재 셀을 기존 전시관 격자의 로컬 중심 좌표로 변환합니다.</summary>
        public Vector2 LocalPosition(VisitObject obj, ExhibitionGrid grid) => grid.CellCenter(obj.Cell);
    }
}
