using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.UI.Exhibition;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Friends
{
    /// <summary>패키지 검증과 방문 전용 재현 상태. 홈의 배치·도깨비 데이터에 접근하지 않습니다.</summary>
    public sealed class ExhibitionVisitScene
    {
        private readonly List<VisitObject> _objects = new List<VisitObject>();
        private readonly List<string> _warnings = new List<string>();
        private readonly int _seed;
        public ExhibitionSnapshot Snapshot { get; }
        public IReadOnlyList<VisitObject> Objects => _objects.AsReadOnly();
        public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();
        /// <summary>스냅샷 규격과 배치를 검증하고 원본과 분리된 방문 객체 상태를 생성합니다.</summary>
        public ExhibitionVisitScene(ExhibitionSnapshot snapshot, Func<bool, int, bool> isKnown = null, int seed = 0)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.OwnerCode)) throw new ArgumentException("전시 정보가 올바르지 않아요.");
            Snapshot = snapshot;
            _seed = seed;
            _warnings.AddRange(snapshot.ParseWarnings);
            if (!snapshot.HasGrid)
            {
                if (snapshot.Placements.Count != 0) throw new ArgumentException("전시 배치 정보를 읽을 수 없어요.");
                return;
            }
            if (snapshot.Width != 7 || snapshot.Height != 7) throw new ArgumentException($"서버 전시는 {snapshot.Width}×{snapshot.Height}입니다. 현재 화면은 7×7 전시만 지원해요.");
            var seen = new HashSet<string>();
            var occupied = new HashSet<Vector2Int>();
            foreach (var row in snapshot.Placements)
            {
                if (row == null || row.Id < 1 || !InBounds(row.Anchor)) { _warnings.Add("잘못된 배치는 표시하지 않았어요."); continue; }
                if (!seen.Add(row.Key)) { _warnings.Add("중복된 배치는 표시하지 않았어요."); continue; }
                var obj = new VisitObject(row, (isKnown ?? IsDefaultKnown)(row.IsBuilding, row.Id));
                if (obj.Unknown) _warnings.Add("아직 지원하지 않는 객체는 임시 모습으로 표시해요.");
                if (!occupied.Add(row.Anchor)) _warnings.Add("같은 칸에 겹친 배치가 있어요. 원본 좌표대로 표시해요.");
                _objects.Add(obj);
            }
        }
        /// <summary>논리 셀이 방문 화면의 7×7 범위 안에 있는지 확인합니다.</summary>
        private static bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < 7 && cell.y < 7;
        /// <summary>표현 카탈로그를 주입하지 않은 호출에서 현재 서버 시드의 콘텐츠 범위를 확인합니다.</summary>
        public static bool IsDefaultKnown(bool isBuilding, int id) => id >= 1 && id <= (isBuilding ? 3 : 9);
        /// <summary>건물과 다른 NPC를 피하는 임시 이동을 진행하며 저장된 배치 앵커는 유지합니다.</summary>
        public void Walk(int step)
        {
            var occupied = new Dictionary<Vector2Int, int>();
            foreach (var obj in _objects)
                occupied[obj.Cell] = occupied.TryGetValue(obj.Cell, out int initialCount) ? initialCount + 1 : 1;
            Vector2Int[] directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
            var random = new System.Random(unchecked(_seed * 486187739 + step * 16777619));
            foreach (var obj in _objects)
            {
                if (obj.Placement.IsBuilding) continue;
                // 이번 보간이 끝날 때까지 모든 시작 셀과 이미 선택한 목적지를 예약합니다.
                // 겹친 시작 배치도 개별 점유를 보존해 다른 객체가 진입하지 못하게 합니다.
                // 시작 방향만 돌리지 않고 모든 후보를 섞어 시드로 재현되는 이동을 만듭니다.
                for (int i = directions.Length - 1; i > 0; i--)
                {
                    int other = random.Next(i + 1);
                    (directions[i], directions[other]) = (directions[other], directions[i]);
                }
                for (int offset = 0; offset < 4; offset++)
                {
                    var target = obj.Cell + directions[offset];
                    if (!InBounds(target) || (occupied.TryGetValue(target, out int count) && count > 0)) continue;
                    obj.MoveTo(target); break;
                }
                occupied[obj.Cell] = occupied.TryGetValue(obj.Cell, out int remaining) ? remaining + 1 : 1;
            }
        }
        /// <summary>방문 객체의 현재 셀을 기존 전시관 격자의 로컬 중심 좌표로 변환합니다.</summary>
        public Vector2 LocalPosition(VisitObject obj, ExhibitionGrid grid) => grid.CellCenter(obj.Cell);
    }
}
