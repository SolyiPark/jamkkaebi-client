using Jamkkaebi.Scripts.UI.Shell;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    public sealed class HousingSurfaceInput : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
        IPointerUpHandler, IDragHandler, IMirrorGestureOwner, IMirrorCancelHandler
    {
        [SerializeField] private float _holdSeconds = .45f;
        private ExhibitionHousing _housing;
        private PointerEventData _pointer;
        private Vector2 _press;
        private float _pressedAt;
        private int _itemId;
        private bool _chosen;
        private bool _validPress;
        private bool _holding;
        private bool _cancelClick;
        private bool _dragging;
        public bool OwnsGesture => _holding && _pointer != null;
        public void Configure(ExhibitionHousing housing) => _housing = housing;
        public void OnPointerDown(PointerEventData data)
        {
            // 앞선 누름이 끝났다는 신호를 받지 못한 채 새 누름이 오면, 끌던 도깨비를 되돌리고 새로 시작한다.
            if (_dragging) CancelHold();
            _cancelClick = false;
            _pointer = data; _press = data.position; _pressedAt = Time.unscaledTime; _chosen = false;
            _itemId = _housing.ItemAtTexturePoint(data.position);
            _validPress = _housing.CanEdit && (_itemId != 0 || _housing.Surface.TryGetCell(_housing.SceneCamera, data.position, out _));
        }

        private void Update()
        {
            if (_pointer == null || !_validPress || OwnsGesture || !_housing.CanEdit) return;
            if (Time.unscaledTime - _pressedAt < _holdSeconds) return;
            _pointer.eligibleForClick = false;
            _cancelClick = true;
            _holding = true;
            _housing.ShowHoldMenu(_press, _itemId);
        }

        public void OnDrag(PointerEventData data)
        {
            if (_pointer == null) return;
            if (!OwnsGesture)
            {
                // 길게 누르기 전에 움직이면 누르기와 클릭을 함께 취소합니다.
                if ((data.position - _press).magnitude > 12)
                { _validPress = false; _cancelClick = true; _pointer.eligibleForClick = false; }
                return;
            }
            // 도깨비의 이동을 고르면 같은 누름으로 끌고 다니다가 손을 뗀 칸에 놓는다.
            if (_dragging) { _housing.DragSpiritTo(data.position); return; }
            if (_chosen) return;
            if (!_housing.Menu.TryChoose(data.position, out var action)) return;
            _chosen = true;
            if (action == HousingAction.Move && _housing.BeginSpiritDrag(_itemId, data.position)) { _dragging = true; return; }
            _housing.ExecuteAction(_itemId, action);
        }

        /// <summary>
        /// 손을 떼지 않았는데 입력이 끊겼을 때(포커스 상실, 탭 전환 등) 누름 종료보다 먼저 불린다.
        /// 끌던 도깨비를 마지막 손가락 위치에 놓지 않고 원래 자리로 되돌린다.
        /// </summary>
        public void OnMirrorCancel() => CancelHold();

        public void OnPointerUp(PointerEventData data)
        {
            if (_dragging && _housing) _housing.EndSpiritDrag(data.position);
            _dragging = false;
            CancelHold();
        }

        private void CancelHold()
        {
            // 손을 떼지 않고 끊긴 끌기(화면 숨김 등)는 놓지 않고 되돌린다.
            if (_dragging && _housing) _housing.CancelSpiritDrag();
            _dragging = false;
            _pointer = null; _itemId = 0; _chosen = false; _validPress = false; _holding = false;
            if (_housing) _housing.HideHoldMenu();
        }
        private void OnDisable() { _cancelClick = true; CancelHold(); }
        public void OnPointerClick(PointerEventData data)
        {
            if (!_housing || _cancelClick || OwnsGesture) return;
            if (_housing.IsEditingCell)
            {
                if (_housing.Surface.TryGetCell(_housing.SceneCamera, data.position, out var cell)) _housing.SelectCell(cell);
            }
            else _housing.SelectAtTexturePoint(data.position);
        }
    }
}
