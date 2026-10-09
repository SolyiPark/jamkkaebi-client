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

        /// <summary>세로 방향이 선택되면 드래그 종료 또는 초기화까지 제스처를 소유합니다.</summary>
        public bool OwnsGesture => _ownsGesture;
        /// <summary>지정된 뷰포트이며, 별도 뷰포트가 없으면 이 컴포넌트의 UI 영역입니다.</summary>
        private RectTransform ViewportRect => viewport ? viewport : (RectTransform)transform;

        /// <summary>왼쪽 포인터의 새 제스처 방향을 초기화하고 이전 스크롤 관성을 멈춥니다.</summary>
        /// <param name="eventData">새 드래그 후보의 포인터 정보입니다.</param>
        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            ResetGesture();
            base.OnInitializePotentialDrag(eventData);
        }

        /// <summary>활성 콘텐츠가 세로 방향을 소유할 때만 기본 스크롤 드래그를 시작합니다.</summary>
        /// <param name="eventData">드래그 시작 위치와 버튼 정보입니다.</param>
        public override void OnBeginDrag(PointerEventData eventData)
        {
            ChooseAxis(eventData);
            if (!OwnsGesture || !IsActive() || eventData.button != PointerEventData.InputButton.Left) return;
            base.OnBeginDrag(eventData);
            _dragStarted = true;
        }

        /// <summary>시작 전 호출에서는 방향만 판단하고, 시작된 세로 드래그에서는 콘텐츠 이동과 위치를 기록합니다.</summary>
        /// <param name="eventData">누름 위치와 현재 위치를 포함한 포인터 정보입니다.</param>
        public override void OnDrag(PointerEventData eventData)
        {
            // MirrorInput queries ownership through OnDrag before dispatching OnBeginDrag.
            ChooseAxis(eventData);
            if (!_dragStarted || !OwnsGesture) return;
            base.OnDrag(eventData);
            RememberLayout(verticalNormalizedPosition);
        }

        /// <summary>왼쪽 포인터 드래그를 종료하고 관성 동작을 유지한 채 제스처 소유권을 해제합니다.</summary>
        /// <param name="eventData">종료할 드래그의 포인터 정보입니다.</param>
        public override void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            base.OnEndDrag(eventData);
            ResetGesture();
        }

        /// <summary>비활성화 시 제스처 상태를 해제하고 기본 스크롤의 드래그 및 관성 상태를 정리합니다.</summary>
        protected override void OnDisable()
        {
            ResetGesture();
            base.OnDisable();
        }

        /// <summary>명시적 스크롤 위치를 적용하고 세로 요청은 현재 레이아웃과 함께 즉시 기록합니다.</summary>
        /// <param name="value">요청한 정규화 스크롤 위치입니다.</param>
        /// <param name="axis">0은 가로축이고 1은 세로축입니다.</param>
        protected override void SetNormalizedPosition(float value, int axis)
        {
            base.SetNormalizedPosition(value, axis);
            if (axis == 1 && content) RememberLayout(value);
        }

        /// <summary>
        /// 동일 콘텐츠의 뷰포트만 변경되면 논리 위치를 복원한 뒤 기본 관성을 갱신합니다.
        /// 스크롤 범위가 잠시 사라져도 이전 위치를 보관하고, 콘텐츠가 바뀌면 새 위치를 기록합니다.
        /// </summary>
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

        /// <summary>콘텐츠 참조와 두 영역의 크기를 저장하고 정규화된 세로 위치를 0에서 1로 제한해 기록합니다.</summary>
        /// <param name="position">다음 뷰포트 변경에서도 유지할 논리 스크롤 위치입니다.</param>
        private void RememberLayout(float position)
        {
            if (!content) return;
            _rememberedContent = content;
            _rememberedContentSize = content.rect.size;
            _rememberedViewportSize = ViewportRect.rect.size;
            _rememberedVerticalPosition = Mathf.Clamp01(position);
            _hasLayoutSnapshot = true;
        }

        /// <summary>최소 이동량을 넘으면 지배적인 방향을 선택하고 한 제스처 동안 선택을 유지합니다.</summary>
        /// <param name="eventData">최초 누름 위치와 현재 위치를 비교할 포인터 정보입니다.</param>
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

        /// <summary>다음 포인터가 방향을 다시 선택할 수 있도록 축 선택, 소유권과 시작 상태를 초기화합니다.</summary>
        private void ResetGesture()
        {
            _axisChosen = false;
            _ownsGesture = false;
            _dragStarted = false;
        }
    }
}
