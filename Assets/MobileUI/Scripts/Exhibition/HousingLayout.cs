using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace MobilePrototype.Exhibition
{
    // Logical occupancy only. Presentation transforms never enter the layout.
    public sealed class HousingLayout
    {
        private readonly ExhibitionGrid _grid;
        private readonly List<HousingPlacement> _placements = new List<HousingPlacement>();
        private readonly HashSet<Vector2Int> _traversal = new HashSet<Vector2Int>();
        private int _nextId = 1;
        public ReadOnlyCollection<HousingPlacement> Placements { get; }

        public HousingLayout(ExhibitionGrid grid)
        { _grid = grid; Placements = _placements.AsReadOnly(); }

        public HousingPlacement At(Vector2Int cell) => _placements.Find(item => item.Contains(cell));
        public HousingPlacement GetPlacement(int id) => _placements.Find(item => item.Id == id);
        public bool IsWalkable(Vector2Int cell) => _grid.Contains(cell) && At(cell) == null;

        public bool CanPlace(Vector2Int cell, Vector2Int size, int ignoredId = 0)
        {
            if (!_grid.ContainsFootprint(cell, size)) return false;
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                {
                    var target = cell + new Vector2Int(x, y);
                    var occupant = At(target);
                    if (_traversal.Contains(target) || (occupant != null && occupant.Id != ignoredId)) return false;
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

        // The first dummy has one walker; a later movement service can own per-agent reservations.
        public bool TryReserveTraversal(Vector2Int from, Vector2Int to)
        {
            if (!IsWalkable(from) || !IsWalkable(to) ||
                Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y) > 1) return false;
            _traversal.Clear();
            _traversal.Add(from);
            _traversal.Add(to);
            return true;
        }

        public void ClearTraversal() => _traversal.Clear();
    }
}
