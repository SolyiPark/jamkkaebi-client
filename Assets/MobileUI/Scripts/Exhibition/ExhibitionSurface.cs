using UnityEngine;

namespace MobilePrototype.Exhibition
{
    public sealed class ExhibitionSurface : MonoBehaviour
    {
        [SerializeField] private ExhibitionGrid _grid;
        [SerializeField] private bool _showEditorGrid;
        public ExhibitionGrid Grid => _grid;

        // texturePoint has already been mapped by MirrorInput. Do not map screen coordinates twice.
        /// <summary>
        /// MirrorInput이 이미 변환한 렌더 텍스처 픽셀 좌표의 광선을 플랫폼 평면에 투영해 유효 셀을 찾습니다. 화면 좌표를 직접 전달하거나 중복 변환하지 않습니다.
        /// </summary>
        public bool TryGetCell(Camera camera, Vector2 texturePoint, out Vector2Int cell)
        {
            cell = default;
            if (!_grid || !camera) return false;
            var ray = camera.ScreenPointToRay(texturePoint);
            var plane = new Plane(transform.forward, transform.position);
            return plane.Raycast(ray, out float distance) &&
                _grid.TryGetCell(transform.InverseTransformPoint(ray.GetPoint(distance)), out cell);
        }
        /// <summary>
        /// 격자 셀 중심에 플랫폼의 현재 표시 변환을 적용해 월드 위치를 반환합니다. 격자 참조와 유효 셀이 필요합니다.
        /// </summary>
        public Vector3 CellWorldPosition(Vector2Int cell) => transform.TransformPoint(_grid.CellCenter(cell));

        /// <summary>
        /// 선택된 오브젝트에서 옵션이 켜진 경우에만 에디터 격자를 그립니다. 게임 화면에는 격자선을 표시하지 않습니다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!_showEditorGrid || !_grid || !_grid.IsValid) return;
            Gizmos.color = Color.cyan;
            var previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int x = 0; x <= Mathf.Min(_grid.Size.x, 128); x++)
                Gizmos.DrawLine(_grid.ToLocal(new Vector2(x, 0)), _grid.ToLocal(new Vector2(x, _grid.Size.y)));
            for (int y = 0; y <= Mathf.Min(_grid.Size.y, 128); y++)
                Gizmos.DrawLine(_grid.ToLocal(new Vector2(0, y)), _grid.ToLocal(new Vector2(_grid.Size.x, y)));
            Gizmos.matrix = previous;
        }
    }
}
