using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    // Logical occupancy only. Presentation transforms never enter the layout.
    public sealed class HousingLayout
    {
        private readonly ExhibitionGrid _grid;
        private readonly List<HousingPlacement> _placements = new List<HousingPlacement>();
        private readonly Dictionary<int, HousingWalker> _walkers = new Dictionary<int, HousingWalker>();
        private int _nextId = 1;
        public ReadOnlyCollection<HousingPlacement> Placements { get; }

        /// <summary>걸어 다니는 객체들. 순서는 정하지 않는다.</summary>
        public IReadOnlyCollection<HousingWalker> Walkers => _walkers.Values;

        public HousingLayout(ExhibitionGrid grid)
        { _grid = grid; Placements = _placements.AsReadOnly(); }

        public HousingPlacement At(Vector2Int cell) => _placements.Find(item => item.Contains(cell));
        public HousingPlacement GetPlacement(int id) => _placements.Find(item => item.Id == id);
        public HousingWalker GetWalker(int id) => _walkers.TryGetValue(id, out var walker) ? walker : null;
        public bool IsWalkable(Vector2Int cell) => _grid.Contains(cell) && At(cell) == null;

        /// <summary>
        /// 고정 배치물을 둘 수 있는지 검사한다. 다른 배치물이나 걷는 객체가 지금 쓰는 칸(서 있는 칸과 걸어 들어가는 칸)과 겹치면 안 된다.
        /// 걷는 객체의 기준 자리는 서 있는 칸이므로 함께 막힌다.
        /// </summary>
        public bool CanPlace(Vector2Int cell, Vector2Int size, int ignoredId = 0)
        {
            if (!_grid.ContainsFootprint(cell, size)) return false;
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                {
                    var target = cell + new Vector2Int(x, y);
                    var occupant = At(target);
                    if (ReservedByWalker(target) || (occupant != null && occupant.Id != ignoredId)) return false;
                }
            return true;
        }

        public bool TryPlace(string itemId, Vector2Int cell, Vector2Int size, out HousingPlacement placement)
        {
            placement = null;
            if (string.IsNullOrWhiteSpace(itemId) || !CanPlace(cell, size)) return false;
            placement = new HousingPlacement(_nextId++, itemId, cell, size);
            _placements.Add(placement);
            return true;
        }

        public bool TryMove(int id, Vector2Int cell)
        {
            var placement = _placements.Find(item => item.Id == id);
            if (placement == null || !CanPlace(cell, placement.Size, id)) return false;
            placement.Cell = cell;
            return true;
        }

        public bool Remove(int id) => _placements.RemoveAll(item => item.Id == id) > 0;

        public bool TryRotate(int id)
        {
            var placement = _placements.Find(item => item.Id == id);
            if (placement == null) return false;
            var rotatedSize = new Vector2Int(placement.Size.y, placement.Size.x);
            if (!CanPlace(placement.Cell, rotatedSize, id)) return false;
            placement.QuarterTurns = (placement.QuarterTurns + 1) % 4;
            return true;
        }

        /// <summary>
        /// 걷는 객체를 세울 수 있는 칸인지 검사한다. 배치물이 없고 다른 걷는 객체가 지금 쓰는 칸이 아니어야 한다.
        /// </summary>
        /// <param name="ignoredWalkerId">자기 자리를 옮길 때 자신을 빼고 검사한다.</param>
        public bool CanAddWalker(Vector2Int cell, int ignoredWalkerId = 0) =>
            IsWalkable(cell) && !ReservedByWalker(cell, ignoredWalkerId);

        /// <summary>걷는 객체를 칸에 세우고 그 칸을 예약한다. 그 칸이 첫 기준 자리다.</summary>
        public bool TryAddWalker(Vector2Int cell, out HousingWalker walker)
        {
            walker = null;
            if (!CanAddWalker(cell)) return false;
            walker = new HousingWalker(_nextId++, cell);
            _walkers.Add(walker.Id, walker);
            return true;
        }

        /// <summary>걷는 객체를 다른 칸으로 옮겨 세운다. 걷던 길 예약은 버린다. 실패하면 아무것도 바꾸지 않는다.</summary>
        public bool TryMoveWalker(int id, Vector2Int cell)
        {
            if (!_walkers.TryGetValue(id, out var walker) || !CanAddWalker(cell, id)) return false;
            walker.From = walker.To = cell;
            return true;
        }

        /// <summary>걷는 객체와 그 예약을 함께 없앤다.</summary>
        public bool RemoveWalker(int id) => _walkers.Remove(id);

        /// <summary>
        /// 한 칸 걷는 길을 예약한다. 두 칸 모두 배치물이 없고 다른 걷는 객체가 쓰지 않아야 한다.
        /// 도착(from과 to가 같음)을 예약하면 그 칸이 새 기준 자리가 된다. 실패하면 기존 예약을 그대로 둔다.
        /// </summary>
        public bool TryReserveTraversal(int walkerId, Vector2Int from, Vector2Int to)
        {
            if (!_walkers.TryGetValue(walkerId, out var walker)) return false;
            if (!IsWalkable(from) || !IsWalkable(to) ||
                Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y) > 1) return false;
            if (ReservedByWalker(from, walkerId) || ReservedByWalker(to, walkerId)) return false;
            walker.From = from; walker.To = to;
            return true;
        }

        private bool ReservedByWalker(Vector2Int cell, int ignoredWalkerId = 0)
        {
            foreach (var walker in _walkers.Values)
                if (walker.Id != ignoredWalkerId && walker.Reserves(cell)) return true;
            return false;
        }
    }
}
