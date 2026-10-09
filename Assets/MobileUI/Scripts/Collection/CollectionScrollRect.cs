using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobilePrototype.Collection
{
    /// <summary>
    /// 세로 스크롤을 소유하고 가로 드래그는 공통 탭 전환에 맡깁니다.
    /// </summary>
    public sealed class CollectionScrollRect : ScrollRect, IMirrorGestureOwner
    {
        private const float GestureThresholdPixels = 8f;
        private const float HorizontalDominance = 1.2f;
        private bool _axisChosen;
        private bool _ownsGesture;
        private bool _dragStarted;
        private RectTransform _rememberedContent;
        private Vector2 _rememberedContentSize;
        private Vector2 _rememberedViewportSize;
        private float _rememberedVerticalPosition = 1;
        private bool _hasLayoutSnapshot;

        public bool OwnsGesture => _ownsGesture;
        private RectTransform ViewportRect => viewport ? viewport : (RectTransform)transform;

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            ResetGesture();
            base.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            ChooseAxis(eventData);
            if (!OwnsGesture || !IsActive() || eventData.button != PointerEventData.InputButton.Left) return;
            base.OnBeginDrag(eventData);
            _dragStarted = true;
        }

        public override void OnDrag(PointerEventData eventData)
        {
            // MirrorInput queries ownership through OnDrag before dispatching OnBeginDrag.
            ChooseAxis(eventData);
            if (!_dragStarted || !OwnsGesture) return;
            base.OnDrag(eventData);
            RememberLayout(verticalNormalizedPosition);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            base.OnEndDrag(eventData);
            ResetGesture();
        }

        protected override void OnDisable()
        {
            ResetGesture();
            base.OnDisable();
        }

        protected override void SetNormalizedPosition(float value, int axis)
        {
            base.SetNormalizedPosition(value, axis);
            if (axis == 1 && content) RememberLayout(value);
        }

        protected override void LateUpdate()
        {
            if (!content)
            {
                _hasLayoutSnapshot = false;
                base.LateUpdate();
                return;
            }
            bool sameContent = _hasLayoutSnapshot && content == _rememberedContent &&
                content.rect.size == _rememberedContentSize;
            bool viewportChanged = sameContent && ViewportRect.rect.size != _rememberedViewportSize;
            // The host resizes hidden tabs too. Preserve the logical location before
            // ScrollRect clamps an old pixel offset to the new viewport bounds.
            if (viewportChanged && !_dragStarted)
                base.SetNormalizedPosition(_rememberedVerticalPosition, 1);
            base.LateUpdate();

            sameContent = _hasLayoutSnapshot && content == _rememberedContent &&
                content.rect.size == _rememberedContentSize;
            float position = !sameContent || content.rect.height > ViewportRect.rect.height + .01f
                ? verticalNormalizedPosition : _rememberedVerticalPosition;
            // A temporarily larger viewport can hide the entire scroll range.
            // Keep the logical position until that range becomes visible again.
            RememberLayout(position);
        }

        private void RememberLayout(float position)
        {
            if (!content) return;
            _rememberedContent = content;
            _rememberedContentSize = content.rect.size;
            _rememberedViewportSize = ViewportRect.rect.size;
            _rememberedVerticalPosition = Mathf.Clamp01(position);
            _hasLayoutSnapshot = true;
        }

        private void ChooseAxis(PointerEventData eventData)
        {
            if (_axisChosen || !IsActive() || !vertical || eventData.button != PointerEventData.InputButton.Left) return;
            var displacement = eventData.position - eventData.pressPosition;
            if (displacement.sqrMagnitude < GestureThresholdPixels * GestureThresholdPixels) return;
            var direction = displacement.normalized;
            float horizontalAmount = Mathf.Abs(direction.x);
            float verticalAmount = Mathf.Abs(direction.y);
            if (verticalAmount > horizontalAmount)
            {
                _axisChosen = true;
                _ownsGesture = true;
            }
            else if (horizontalAmount > verticalAmount * HorizontalDominance)
            {
                _axisChosen = true;
            }
        }

        private void ResetGesture()
        {
            _axisChosen = false;
            _ownsGesture = false;
            _dragStarted = false;
        }
    }
}
