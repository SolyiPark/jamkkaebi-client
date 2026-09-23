using UnityEngine;

namespace MobilePrototype.Exhibition
{
    [CreateAssetMenu(menuName = "Jamkkaebi/Exhibition/Grid")]
    public sealed class ExhibitionGrid : ScriptableObject
    {
        [SerializeField] private Vector2 _origin = new Vector2(0, .675f);
        [SerializeField] private Vector2 _axisU = new Vector2(.3f, -.17142857f);
        [SerializeField] private Vector2 _axisV = new Vector2(-.3f, -.17142857f);
        [SerializeField] private Vector2Int _size = new Vector2Int(7, 7);
        [SerializeField] private Vector2Int[] _excludedCells = new Vector2Int[0];
        public Vector2 Origin => _origin;
        public Vector2 AxisU => _axisU;
        public Vector2 AxisV => _axisV;
        public Vector2Int Size => _size;
        public bool IsValid => _size.x > 0 && _size.y > 0 && Mathf.Abs(Determinant) > .000001f;
        private float Determinant => _axisU.x * _axisV.y - _axisU.y * _axisV.x;

        public Vector2 ToLocal(Vector2 grid) => _origin + grid.x * _axisU + grid.y * _axisV;
        public Vector2 CellCenter(Vector2Int cell) => ToLocal((Vector2)cell + Vector2.one * .5f);
        public bool TryToGrid(Vector2 local, out Vector2 grid)
        {
            grid = default;
            if (!IsValid) return false;
            var point = local - _origin;
            grid = new Vector2((point.x * _axisV.y - point.y * _axisV.x) / Determinant,
                (_axisU.x * point.y - _axisU.y * point.x) / Determinant);
            return float.IsFinite(grid.x) && float.IsFinite(grid.y);
        }
        public bool Contains(Vector2Int cell)
        {
            if (!IsValid || cell.x < 0 || cell.y < 0 || cell.x >= _size.x || cell.y >= _size.y) return false;
            foreach (var excluded in _excludedCells) if (excluded == cell) return false;
            return true;
        }
        public bool TryGetCell(Vector2 local, out Vector2Int cell)
        {
            cell = default;
            if (!TryToGrid(local, out var grid)) return false;
            cell = Vector2Int.FloorToInt(grid);
            return Contains(cell);
        }
        public bool ContainsFootprint(Vector2Int anchor, Vector2Int size)
        {
            if (size.x <= 0 || size.y <= 0 || size.x > _size.x || size.y > _size.y) return false;
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                    if (!Contains(anchor + new Vector2Int(x, y))) return false;
            return true;
        }
    }
}
