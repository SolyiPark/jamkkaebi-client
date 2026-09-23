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

        /// <summary>
        /// 연속 격자 좌표를 원점과 두 축으로 합성해 플랫폼 로컬 좌표로 변환합니다. 유효 셀 범위는 검사하지 않습니다.
        /// </summary>
        public Vector2 ToLocal(Vector2 grid) => _origin + grid.x * _axisU + grid.y * _axisV;
        /// <summary>
        /// 정수 셀의 두 축에 각각 0.5를 더해 플랫폼 로컬 중심점을 반환합니다. 셀 유효성은 호출자가 확인해야 합니다.
        /// </summary>
        public Vector2 CellCenter(Vector2Int cell) => ToLocal((Vector2)cell + Vector2.one * .5f);
        /// <summary>
        /// 플랫폼 로컬 좌표를 두 축 행렬의 역변환으로 연속 격자 좌표로 바꿉니다. 잘못된 격자나 비유한 결과는 실패하며, 셀 경계는 검사하지 않습니다.
        /// </summary>
        public bool TryToGrid(Vector2 local, out Vector2 grid)
        {
            grid = default;
            if (!IsValid) return false;
            var point = local - _origin;
            grid = new Vector2((point.x * _axisV.y - point.y * _axisV.x) / Determinant,
                (_axisU.x * point.y - _axisU.y * point.x) / Determinant);
            return float.IsFinite(grid.x) && float.IsFinite(grid.y);
        }
        /// <summary>
        /// 격자가 유효하고 셀이 범위 안에 있으며 제외 셀에 포함되지 않을 때만 참을 반환합니다.
        /// </summary>
        public bool Contains(Vector2Int cell)
        {
            if (!IsValid || cell.x < 0 || cell.y < 0 || cell.x >= _size.x || cell.y >= _size.y) return false;
            foreach (var excluded in _excludedCells) if (excluded == cell) return false;
            return true;
        }
        /// <summary>
        /// 플랫폼 로컬 좌표를 격자 좌표로 변환하고 내림해 셀을 찾습니다. 범위 밖이나 제외 셀은 보정하지 않고 실패하며 출력 셀은 성공할 때만 유효합니다.
        /// </summary>
        public bool TryGetCell(Vector2 local, out Vector2Int cell)
        {
            cell = default;
            if (!TryToGrid(local, out var grid)) return false;
            cell = Vector2Int.FloorToInt(grid);
            return Contains(cell);
        }
        /// <summary>
        /// 앵커에서 양의 두 축 방향으로 펼친 사각 영역의 모든 셀이 유효한지 검사합니다. 다른 배치물의 점유 여부는 검사하지 않습니다.
        /// </summary>
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
