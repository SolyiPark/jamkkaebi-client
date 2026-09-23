using UnityEngine;

namespace MobilePrototype.Exhibition
{
    public sealed class ExhibitionSurface : MonoBehaviour
    {
        [SerializeField] private ExhibitionGrid _grid;
        [SerializeField] private bool _showEditorGrid;
        public ExhibitionGrid Grid => _grid;

        // texturePoint has already been mapped by MirrorInput. Do not map screen coordinates twice.
        public bool TryGetCell(Camera camera, Vector2 texturePoint, out Vector2Int cell)
        {
            cell = default;
            if (!_grid || !camera) return false;
            var ray = camera.ScreenPointToRay(texturePoint);
            var plane = new Plane(transform.forward, transform.position);
            return plane.Raycast(ray, out float distance) &&
                _grid.TryGetCell(transform.InverseTransformPoint(ray.GetPoint(distance)), out cell);
        }
        public Vector3 CellWorldPosition(Vector2Int cell) => transform.TransformPoint(_grid.CellCenter(cell));

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
