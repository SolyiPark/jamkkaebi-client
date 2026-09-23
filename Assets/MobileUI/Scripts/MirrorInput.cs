using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MobilePrototype
{
    // One pointer is captured until release. Other fingers are ignored.
    // Raycasts only the active scene; captured drags may leave the viewport.
    public class MirrorInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public MirrorSceneRoot Target { get; private set; }
        private PointerEventData forwarded;
        private int? pointer;
        private GameObject pressed, dragged;
        private bool dragging;
        private TabHost _host;
        private bool _swiping;
        private bool _verticalGesture;
        private Vector2 _screenPress;
        private Vector2 _lastScreen;
        private float _lastTime;
        private float _velocity;
        /// <summary>
        /// 수평 제스처를 탭 전환으로 전달할 호스트를 연결합니다.
        /// </summary>
        public void ConfigureNavigation(TabHost host) => _host = host;
        private float ViewportScreenWidth => Mathf.Max(1, ((RectTransform)transform).rect.width * GetComponentInParent<Canvas>().scaleFactor);
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        /// <summary>
        /// 진행 중인 입력을 취소한 뒤 전달 대상 씬을 교체합니다. null을 전달하면 대상 연결을 해제합니다.
        /// </summary>
        public void Bind(MirrorSceneRoot target) { Cancel(); Target = target; }
        /// <summary>
        /// 실제 화면 좌표를 대상 카메라의 렌더 텍스처 픽셀 좌표로 변환합니다. 유효한 대상과 텍스처가 필요하며 뷰포트 밖 드래그도 보존합니다.
        /// </summary>
        public Vector2 MapPosition(Vector2 screenPosition, Camera eventCamera)
        {
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, eventCamera, out var p);
            var bounds = rect.rect;
            var texture = Target.sceneCamera.targetTexture;
            return new Vector2((p.x - bounds.xMin) / bounds.width * texture.width,
                (p.y - bounds.yMin) / bounds.height * texture.height);
        }
        /// <summary>
        /// 포인터 위치와 이동량을 렌더 텍스처 좌표로 갱신하고 해당 위치의 대상 씬 충돌 결과를 저장합니다.
        /// </summary>
        private void UpdatePosition(PointerEventData input)
        {
            var next = MapPosition(input.position, input.pressEventCamera);
            forwarded.delta = next - forwarded.position;
            forwarded.position = next;
            forwarded.pointerCurrentRaycast = Raycast(next);
        }
        /// <summary>
        /// 대상 씬의 UI를 정렬 순서와 깊이로 우선 검사하고, 없으면 해당 씬의 2D 물리 공간과 렌더 레이어에서 충돌체를 찾습니다.
        /// </summary>
        public RaycastResult Raycast(Vector2 position)
        {
            hits.Clear();
            var data = new PointerEventData(EventSystem.current) { position = position };
            foreach (var raycaster in Target.uiRaycasters)
                if (raycaster && raycaster.gameObject.activeInHierarchy) raycaster.Raycast(data, hits);
            if (hits.Count > 0)
            {
                hits.Sort((a,b) => {
                    int order = b.sortingOrder.CompareTo(a.sortingOrder);
                    return order != 0 ? order : b.depth.CompareTo(a.depth);
                });
                return hits[0];
            }
            var camera = Target.sceneCamera;
            var ray = camera.ScreenPointToRay(position);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (plane.Raycast(ray, out var distance))
            {
                var world = ray.GetPoint(distance);
                var collider = Target.gameObject.scene.GetPhysicsScene2D().OverlapPoint(world, 1 << Target.RenderLayer);
                if (collider) return new RaycastResult { gameObject = collider.gameObject, worldPosition = world, screenPosition = position };
            }
            return new RaycastResult();
        }
        /// <summary>
        /// 전환 중이 아닐 때 첫 왼쪽 포인터를 캡처하고, 대상 씬의 누름 처리와 전용 드래그 핸들러를 준비합니다. 추가 포인터는 무시합니다.
        /// </summary>
        public void OnPointerDown(PointerEventData input)
        {
            if (pointer.HasValue || !Target || !Target.sceneCamera.targetTexture || (_host && _host.IsBusy)) return;
            if (input.button != PointerEventData.InputButton.Left) return;
            pointer = input.pointerId;
            _screenPress = _lastScreen = input.position;
            _lastTime = Time.unscaledTime;
            _velocity = 0;
            _verticalGesture = false;
            forwarded = new PointerEventData(EventSystem.current) { pointerId = input.pointerId, button = input.button, eligibleForClick = true };
            UpdatePosition(input);
            forwarded.delta = Vector2.zero;
            forwarded.pressPosition = forwarded.position;
            forwarded.pointerPressRaycast = forwarded.pointerCurrentRaycast;
            var hit = forwarded.pointerCurrentRaycast.gameObject;
            pressed = ExecuteEvents.ExecuteHierarchy(hit, forwarded, ExecuteEvents.pointerDownHandler);
            if (!pressed) pressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            forwarded.pointerPress = pressed;
            forwarded.rawPointerPress = hit;
            dragged = ExecuteEvents.GetEventHandler<IDragHandler>(hit);
            forwarded.pointerDrag = dragged;
            if (dragged) ExecuteEvents.Execute(dragged, forwarded, ExecuteEvents.initializePotentialDrag);
        }
        /// <summary>
        /// EventSystem의 드래그 거리 임계값을 사용하도록 설정합니다.
        /// </summary>
        public void OnInitializePotentialDrag(PointerEventData data) { data.useDragThreshold = true; }
        /// <summary>
        /// EventSystem의 시작 콜백을 수신합니다. 실제 대상 드래그 시작은 제스처 소유권을 판단하는 OnDrag에서 전달합니다.
        /// </summary>
        public void OnBeginDrag(PointerEventData data) { }
        /// <summary>
        /// EventSystem의 종료 콜백을 수신합니다. 실제 대상 종료와 상태 해제는 OnPointerUp 또는 Cancel에서 처리합니다.
        /// </summary>
        public void OnEndDrag(PointerEventData data) { }
        /// <summary>
        /// 전용 콘텐츠 드래그를 우선하고 일반 수평 제스처만 탭 스와이프로 전환합니다. 스와이프가 시작되면 기존 클릭을 취소합니다.
        /// </summary>
        public void OnDrag(PointerEventData input)
        {
            if (pointer != input.pointerId || forwarded == null || !Target) return;
            var screenDelta = input.position - _screenPress;
            float now = Time.unscaledTime;
            _velocity = (input.position.x - _lastScreen.x) / Mathf.Max(.016f, now - _lastTime) / ViewportScreenWidth;
            _lastScreen = input.position; _lastTime = now;
            // Dedicated draggable objects retain their gesture. Empty areas and buttons allow tab swipes.
            if (!_swiping && !dragged && !_verticalGesture && _host)
            {
                float threshold = Mathf.Max(12, ViewportScreenWidth * .018f);
                if (Mathf.Abs(screenDelta.y) > threshold && Mathf.Abs(screenDelta.y) > Mathf.Abs(screenDelta.x))
                    _verticalGesture = true;
                else if (Mathf.Abs(screenDelta.x) > threshold && Mathf.Abs(screenDelta.x) > Mathf.Abs(screenDelta.y) * 1.2f)
                {
                    _swiping = _host.BeginSwipe(screenDelta.x < 0 ? 1 : -1);
                    if (_swiping)
                    {
                        forwarded.eligibleForClick = false;
                        if (pressed) ExecuteEvents.Execute(pressed, forwarded, ExecuteEvents.pointerUpHandler);
                        pressed = null;
                    }
                }
            }
            if (_swiping)
            {
                _host.Transition.Drag(screenDelta.x / ViewportScreenWidth);
                return;
            }
            UpdatePosition(input);
            if (!dragging)
            {
                dragging = true;
                forwarded.dragging = true;
                forwarded.eligibleForClick = false;
                if (dragged) ExecuteEvents.Execute(dragged, forwarded, ExecuteEvents.beginDragHandler);
            }
            if (dragged) ExecuteEvents.Execute(dragged, forwarded, ExecuteEvents.dragHandler);
        }
        /// <summary>
        /// 캡처한 포인터를 해제해 스와이프를 정착시키거나 대상 클릭·드래그 종료를 전달합니다. 오래 멈춘 스와이프의 속도는 0으로 처리합니다.
        /// </summary>
        public void OnPointerUp(PointerEventData input)
        {
            if (pointer != input.pointerId || forwarded == null || !Target) return;
            if (_swiping)
            {
                float velocity = Time.unscaledTime - _lastTime > .12f ? 0 : _velocity;
                Clear();
                _host.Transition.Release(velocity);
                return;
            }
            UpdatePosition(input);
            if (pressed) ExecuteEvents.Execute(pressed, forwarded, ExecuteEvents.pointerUpHandler);
            var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(forwarded.pointerCurrentRaycast.gameObject);
            if (!dragging && pressed && pressed == click)
                ExecuteEvents.Execute(pressed, forwarded, ExecuteEvents.pointerClickHandler);
            if (dragging && dragged) ExecuteEvents.Execute(dragged, forwarded, ExecuteEvents.endDragHandler);
            Clear();
        }
        /// <summary>
        /// 클릭을 발생시키지 않고 누름·드래그를 종료하며 진행 중인 탭 스와이프를 원래 탭으로 취소합니다.
        /// </summary>
        public void Cancel()
        {
            bool cancelSwipe = _swiping;
            _swiping = false;
            if (forwarded != null)
            {
                forwarded.eligibleForClick = false;
                if (pressed) ExecuteEvents.Execute(pressed, forwarded, ExecuteEvents.pointerUpHandler);
                if (dragging && dragged) ExecuteEvents.Execute(dragged, forwarded, ExecuteEvents.endDragHandler);
            }
            Clear();
            if (cancelSwipe && _host) _host.Transition.Cancel();
        }
        /// <summary>
        /// 포인터 소유권과 제스처 상태를 초기화합니다. 대상 이벤트 전달과 전환 종료는 호출자가 처리합니다.
        /// </summary>
        private void Clear() { _swiping = false; _verticalGesture = false; pointer = null; forwarded = null; pressed = dragged = null; dragging = false; }
        /// <summary>
        /// 입력 브리지가 비활성화될 때 캡처된 포인터와 진행 중 전환을 취소합니다.
        /// </summary>
        private void OnDisable() => Cancel();
        /// <summary>
        /// 앱이 포커스를 잃으면 미완료 입력을 취소해 복귀 후 클릭이나 드래그가 남지 않도록 합니다.
        /// </summary>
        private void OnApplicationFocus(bool focus) { if (!focus) Cancel(); }
    }
}
