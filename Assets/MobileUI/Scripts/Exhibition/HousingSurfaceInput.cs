using UnityEngine;
using UnityEngine.EventSystems;

namespace MobilePrototype.Exhibition
{
    public sealed class HousingSurfaceInput : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
        IPointerUpHandler, IDragHandler, IMirrorGestureOwner
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
        public bool OwnsGesture => _holding && _pointer != null;
        public void Configure(ExhibitionHousing housing) => _housing = housing;
        public void OnPointerDown(PointerEventData data)
        {
            _pointer = data; _press = data.position; _pressedAt = Time.unscaledTime; _chosen = false;
            _itemId = _housing.ItemAtTexturePoint(data.position);
            _validPress = _itemId != 0 || _housing.Surface.TryGetCell(_housing.SceneCamera, data.position, out _);
        }

        private void Update()
        {
            if (_pointer == null || !_validPress || OwnsGesture) return;
            if (Time.unscaledTime - _pressedAt < _holdSeconds) return;
            _pointer.eligibleForClick = false;
            _holding = true;
            _housing.ShowHoldMenu(_press, _itemId);
        }

        public void OnDrag(PointerEventData data)
        {
            if (_pointer == null) return;
            if (!OwnsGesture)
            {
                // Moving before the hold threshold abandons the hold and allows tab navigation.
                if ((data.position - _press).magnitude > 12) _validPress = false;
                return;
            }
            if (_chosen) return;
            if (!_housing.Menu.TryChoose(data.position, out var action)) return;
            _chosen = true;
            _housing.ExecuteAction(_itemId, action);
        }

        public void OnPointerUp(PointerEventData data) => CancelHold();
        private void CancelHold()
        {
            _pointer = null; _itemId = 0; _chosen = false; _validPress = false; _holding = false;
            if (_housing) _housing.HideHoldMenu();
        }
        private void OnDisable() => CancelHold();
        public void OnPointerClick(PointerEventData data)
        {
            if (_housing.Surface.TryGetCell(_housing.SceneCamera, data.position, out var cell))
                _housing.SelectCell(cell);
        }
    }
}
