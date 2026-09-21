using UnityEngine;
using UnityEngine.EventSystems;

namespace MobilePrototype
{
    public class DemoDraggable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Camera sceneCamera;
        public int dragCount;
        private Vector3 offset;
        private Vector3 originalScale;
        private void Awake() => originalScale = transform.localScale;
        private Vector3 World(Vector2 point)
        {
            var ray = sceneCamera.ScreenPointToRay(point);
            var plane = new Plane(Vector3.forward, new Vector3(0,0,transform.position.z));
            return plane.Raycast(ray, out var d) ? ray.GetPoint(d) : transform.position;
        }
        public void OnPointerDown(PointerEventData e) { offset = transform.position - World(e.position); transform.localScale = originalScale * 1.06f; }
        public void OnPointerUp(PointerEventData e) => transform.localScale = originalScale;
        public void OnBeginDrag(PointerEventData e) { dragCount++; }
        public void OnDrag(PointerEventData e)
        {
            var p = World(e.position) + offset;
            float halfWidth = sceneCamera.orthographicSize * sceneCamera.aspect;
            p.x = Mathf.Clamp(p.x, -halfWidth + 0.6f, halfWidth - 0.6f);
            p.y = Mathf.Clamp(p.y, -sceneCamera.orthographicSize + 0.6f, sceneCamera.orthographicSize - 0.6f);
            transform.position = p;
        }
        public void OnEndDrag(PointerEventData e) => transform.localScale = originalScale;
    }
}
