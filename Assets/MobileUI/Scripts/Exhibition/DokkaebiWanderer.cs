using UnityEngine;

namespace MobilePrototype.Exhibition
{
    // Replace this adjacent-cell policy when richer movement is introduced.
    public sealed class DokkaebiWanderer : MonoBehaviour
    {
        [SerializeField] private float _secondsPerCell = .85f;
        [SerializeField] private float _idleSeconds = .7f;
        private HousingLayout _layout;
        private ExhibitionSurface _surface;
        private Vector2Int _from;
        private Vector2Int _to;
        private float _progress;
        private float _idle;
        public Vector2Int CurrentCell => _from;
        public Vector2Int PlacementCell { get; private set; }
        public Vector2Int TargetCell => _to;
        public int CompletedSteps { get; private set; }
        private static readonly Vector2Int[] Directions =
            { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

        /// <summary>이동에 사용할 하우징·표면과 저장 기준점을 지정하고 시작 셀을 예약합니다.</summary>
        public void Configure(HousingLayout layout, ExhibitionSurface surface, Vector2Int cell)
        {
            _layout = layout; _surface = surface; _from = _to = cell;
            PlacementCell = cell;
            _layout.TryReserveTraversal(cell, cell);
            transform.position = _surface.CellWorldPosition(cell);
        }

        /// <summary>인접한 빈 셀을 예약해 걷기 애니메이션을 진행하며 저장 기준점은 변경하지 않습니다.</summary>
        private void Update()
        {
            if (_layout == null) return;
            if (_from == _to)
            {
                _idle -= Time.deltaTime;
                if (_idle > 0) return;
                _idle = Mathf.Max(.1f, _idleSeconds);
                int start = Random.Range(0, Directions.Length);
                for (int i = 0; i < Directions.Length; i++)
                {
                    var next = _from + Directions[(start + i) % Directions.Length];
                    if (!_layout.TryReserveTraversal(_from, next)) continue;
                    _to = next; _progress = 0;
                    break;
                }
            }
            if (_from != _to)
            {
                _progress = Mathf.Min(1, _progress + Time.deltaTime / Mathf.Max(.1f, _secondsPerCell));
                transform.position = Vector3.Lerp(_surface.CellWorldPosition(_from),
                    _surface.CellWorldPosition(_to), _progress);
                if (_progress >= 1)
                {
                    _from = _to;
                    _layout.TryReserveTraversal(_from, _from);
                    CompletedSteps++;
                }
            }
        }

        /// <summary>NPC 종료 시 하우징에 남은 이동 경로 예약을 해제합니다.</summary>
        private void OnDestroy() => _layout?.ClearTraversal();
    }
}
