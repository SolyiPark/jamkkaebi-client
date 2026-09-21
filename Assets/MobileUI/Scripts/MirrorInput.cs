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
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        public void Bind(MirrorSceneRoot target) { Cancel(); Target = target; }
        public Vector2 MapPosition(Vector2 screenPosition, Camera eventCamera)
        {
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPosition, eventCamera, out var p);
            var bounds = rect.rect;
            var texture = Target.sceneCamera.targetTexture;
            return new Vector2((p.x - bounds.xMin) / bounds.width * texture.width,
                (p.y - bounds.yMin) / bounds.height * texture.height);
        }
        private void UpdatePosition(PointerEventData input)
        {
            var next = MapPosition(input.position, input.pressEventCamera);
            forwarded.delta = next - forwarded.position;
            forwarded.position = next;
            forwarded.pointerCurrentRaycast = Raycast(next);
        }
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
        public void OnPointerDown(PointerEventData input)
        {
            if (pointer.HasValue || !Target || !Target.sceneCamera.targetTexture) return;
            if (input.button != PointerEventData.InputButton.Left) return;
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
        public void OnInitializePotentialDrag(PointerEventData data) { data.useDragThreshold = true; }
        public void OnBeginDrag(PointerEventData data) { }
        public void OnEndDrag(PointerEventData data) { }
        public void OnDrag(PointerEventData input)
        {
            if (pointer != input.pointerId || forwarded == null || !Target) return;
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
        public void OnPointerUp(PointerEventData input)
        {
            if (pointer != input.pointerId || forwarded == null || !Target) return;
            UpdatePosition(input);
            if (pressed) ExecuteEvents.Execute(pressed, forwarded, ExecuteEvents.pointerUpHandler);
            var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(forwarded.pointerCurrentRaycast.gameObject);
            if (!dragging && pressed && pressed == click)
                ExecuteEvents.Execute(pressed, forwarded, ExecuteEvents.pointerClickHandler);
            if (dragging && dragged) ExecuteEvents.Execute(dragged, forwarded, ExecuteEvents.endDragHandler);
            Clear();
        }
        public void Cancel()
        {
            if (forwarded != null)
            {
                forwarded.eligibleForClick = false;
                if (pressed) ExecuteEvents.Execute(pressed, forwarded, ExecuteEvents.pointerUpHandler);
                if (dragging && dragged) ExecuteEvents.Execute(dragged, forwarded, ExecuteEvents.endDragHandler);
            }
            Clear();
        }
        private void Clear() { pointer = null; forwarded = null; pressed = dragged = null; dragging = false; }
        private void OnDisable() => Cancel();
        private void OnApplicationFocus(bool focus) { if (!focus) Cancel(); }
    }
}
