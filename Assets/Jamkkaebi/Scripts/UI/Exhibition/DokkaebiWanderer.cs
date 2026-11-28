using System;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    // Replace this adjacent-cell policy when richer movement is introduced.
    public sealed class DokkaebiWanderer : MonoBehaviour
    {
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");
        private static readonly Vector2Int[] Directions =
            { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

        [SerializeField] private float _secondsPerCell = .85f;
        [SerializeField] private float _idleSeconds = .7f;
        private HousingLayout _layout;
        private HousingWalker _walker;
        private ExhibitionSurface _surface;
        private Vector2Int _from;
        private Vector2Int _to;
        private float _progress;
        private float _idle;
        private ExhibitionBehaviorPolicy _policy;
        private Animator[] _animators = Array.Empty<Animator>();

        /// <summary>하우징 배치 인스턴스 ID. 등록 전이면 0.</summary>
        public int WalkerId => _walker?.Id ?? 0;

        /// <summary>이 도깨비가 깃든 유물의 relicId. 정령과 묶이지 않았으면 0.</summary>
        public int RelicId { get; private set; }

        public Vector2Int CurrentCell => _from;
        public Vector2Int PlacementCell { get; private set; }
        public Vector2Int TargetCell => _to;
        public int CompletedSteps { get; private set; }

        /// <summary>멈춰 있는지. 멈추면 걷던 자리에 그대로 서고 예약한 칸은 유지한다.</summary>
        public bool Paused { get; private set; }

        /// <summary>이동에 사용할 하우징·표면과 저장 기준점을 지정하고 시작 셀을 예약합니다.</summary>
        /// <returns>기준 자리를 쓸 수 없으면(배치물·다른 도깨비와 겹침) false이며 아무것도 등록하지 않습니다.</returns>
        public bool Configure(HousingLayout layout, ExhibitionSurface surface, Vector2Int cell,
            ExhibitionBehaviorPolicy policy = null, int relicId = 0)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (!layout.TryAddWalker(cell, out var walker)) return false;
            _policy = policy;
            _layout = layout; _walker = walker; _surface = surface; _from = _to = cell;
            PlacementCell = cell; RelicId = relicId;
            _animators = GetComponentsInChildren<Animator>(true);
            transform.position = _surface.CellWorldPosition(cell);
            return true;
        }

        /// <summary>배회를 멈추거나 다시 시작합니다. 걷는 중에 멈추면 그 자리에 서고, 풀면 가던 칸으로 마저 걷습니다.</summary>
        public void SetPaused(bool paused)
        {
            Paused = paused;
            if (paused) SetWalking(false);
        }

        /// <summary>저장 기준 자리를 옮기고 그 칸에 세웁니다. 걷던 길 예약은 버립니다.</summary>
        /// <returns>옮길 수 없는 칸이면 false이며 자리와 예약을 바꾸지 않습니다.</returns>
        public bool TryMoveAnchor(Vector2Int cell)
        {
            if (_layout == null || !_layout.TryMoveWalker(_walker.Id, cell)) return false;
            _from = _to = cell; _progress = 0;
            PlacementCell = cell;
            transform.position = _surface.CellWorldPosition(cell);
            SetWalking(false);
            return true;
        }

        /// <summary>인접한 빈 셀을 예약해 걷기 애니메이션을 진행하며 저장 기준점은 변경하지 않습니다.</summary>
        private void Update()
        {
            if (_layout == null || Paused || (_policy && !_policy.EnableAmbientMovement)) return;
            if (_from == _to)
            {
                _idle -= Time.deltaTime;
                if (_idle > 0) return;
                _idle = Mathf.Max(.1f, _idleSeconds);
                int start = UnityEngine.Random.Range(0, Directions.Length);
                for (int i = 0; i < Directions.Length; i++)
                {
                    var next = _from + Directions[(start + i) % Directions.Length];
                    if (!_layout.TryReserveTraversal(_walker.Id, _from, next)) continue;
                    _to = next; _progress = 0;
                    break;
                }
            }
            if (_from != _to)
            {
                SetWalking(true);
                _progress = Mathf.Min(1, _progress + Time.deltaTime / Mathf.Max(.1f, _secondsPerCell));
                transform.position = Vector3.Lerp(_surface.CellWorldPosition(_from),
                    _surface.CellWorldPosition(_to), _progress);
                if (_progress >= 1)
                {
                    _from = _to;
                    _layout.TryReserveTraversal(_walker.Id, _from, _from);
                    CompletedSteps++;
                    SetWalking(false);
                }
            }
        }

        private void SetWalking(bool walking)
        {
            foreach (var animator in _animators)
                if (animator && animator.isActiveAndEnabled) animator.SetBool(IsMoving, walking);
        }

        /// <summary>NPC 종료 시 하우징에 남은 기준 자리와 이동 경로 예약을 해제합니다.</summary>
        private void OnDestroy()
        {
            if (_layout != null && _walker != null) _layout.RemoveWalker(_walker.Id);
        }
    }
}
