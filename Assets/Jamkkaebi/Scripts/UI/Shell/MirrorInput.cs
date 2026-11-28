using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Jamkkaebi.Scripts.UI.Shell
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
        /// <summary>
        /// 전환 중 입력 차단과 활성 탭 표시 영역을 제공할 호스트를 연결합니다.
        /// </summary>
        public void ConfigureHost(TabHost host) => _host = host;
        /// <summary>
        /// 화면 좌표를 렌더 텍스처로 옮길 기준 영역입니다. 호스트가 있으면 활성 탭 이미지, 없으면 이 입력 표면입니다.
        /// 프로필 바가 있는 탭은 입력 표면보다 위쪽이 짧으므로 이미지 영역을 기준으로 해야 좌표가 맞습니다.
        /// </summary>
        private RectTransform MappingRect => _host && _host.mirror ? _host.DisplayRect : (RectTransform)transform;
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
            var rect = MappingRect;
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
        /// 전환 중이 아닐 때 탭 화면 안에서 누른 첫 왼쪽 포인터를 캡처하고, 대상 씬의 누름 처리와 전용 드래그 핸들러를 준비합니다.
        /// 추가 포인터와 탭 화면 밖(프로필 영역)의 누름은 무시합니다.
        /// </summary>
        public void OnPointerDown(PointerEventData input)
        {
            if (pointer.HasValue || !Target || !Target.sceneCamera.targetTexture || (_host && _host.IsBusy)) return;
            if (input.button != PointerEventData.InputButton.Left) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(MappingRect, input.position, input.pressEventCamera)) return;
            pointer = input.pointerId;
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
        /// 콘텐츠의 제스처 소유권을 확인한 뒤 드래그를 대상 씬에 전달합니다. 드래그는 탭을 바꾸지 않습니다.
        /// 조건부 소유자가 축을 판별하는 동안에는 드래그 시작을 보류하고 클릭만 취소합니다.
        /// </summary>
        /// <param name="input">공통 뷰포트에서 캡처한 포인터의 현재 화면 좌표와 이동 정보입니다.</param>
        public void OnDrag(PointerEventData input)
        {
            if (pointer != input.pointerId || forwarded == null || !Target) return;
            var owner = dragged ? dragged.GetComponent<IMirrorGestureOwner>() : null;
            if (owner != null && !owner.OwnsGesture)
            {
                // 조건부 소유자는 이 호출에서 축을 고릅니다. 아직 소유하지 않으면 드래그를 시작하지 않습니다:
                // dragging을 먼저 켜면 나중에 세로 소유가 정해져도 시작 콜백을 놓칩니다.
                UpdatePosition(input);
                ExecuteEvents.Execute(dragged, forwarded, ExecuteEvents.dragHandler);
                if (!owner.OwnsGesture)
                {
                    forwarded.eligibleForClick = false;
                    return;
                }
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
        /// 캡처한 포인터를 해제하고 대상 클릭·드래그 종료를 전달합니다.
        /// </summary>
        public void OnPointerUp(PointerEventData input)
        {
            if (pointer != input.pointerId || forwarded == null || !Target) return;
            UpdatePosition(input);
            if (pressed) ExecuteEvents.Execute(pressed, forwarded, ExecuteEvents.pointerUpHandler);
            var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(forwarded.pointerCurrentRaycast.gameObject);
            if (!dragging && forwarded.eligibleForClick && pressed && pressed == click)
                ExecuteEvents.Execute(pressed, forwarded, ExecuteEvents.pointerClickHandler);
            if (dragging && dragged) ExecuteEvents.Execute(dragged, forwarded, ExecuteEvents.endDragHandler);
            Clear();
        }
        /// <summary>
        /// 클릭을 발생시키지 않고 누름·드래그를 종료합니다.
        /// </summary>
        public void Cancel()
        {
            if (forwarded != null)
            {
                forwarded.eligibleForClick = false;
                // 누름 종료만으로는 손을 뗀 것과 구분되지 않으므로, 취소임을 먼저 알린다.
                if (pressed)
                    foreach (var handler in pressed.GetComponents<IMirrorCancelHandler>()) handler.OnMirrorCancel();
                if (pressed) ExecuteEvents.Execute(pressed, forwarded, ExecuteEvents.pointerUpHandler);
                if (dragging && dragged) ExecuteEvents.Execute(dragged, forwarded, ExecuteEvents.endDragHandler);
            }
            Clear();
        }
        /// <summary>
        /// 포인터 소유권과 제스처 상태를 초기화합니다. 대상 이벤트 전달은 호출자가 처리합니다.
        /// </summary>
        private void Clear() { pointer = null; forwarded = null; pressed = dragged = null; dragging = false; }
        /// <summary>
        /// 입력 브리지가 비활성화될 때 캡처된 포인터를 취소합니다.
        /// </summary>
        private void OnDisable() => Cancel();
        /// <summary>
        /// 앱이 포커스를 잃으면 미완료 입력을 취소해 복귀 후 클릭이나 드래그가 남지 않도록 합니다.
        /// </summary>
        private void OnApplicationFocus(bool focus) { if (!focus) Cancel(); }
    }
}
