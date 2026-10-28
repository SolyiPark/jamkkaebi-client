using UnityEngine;
using UnityEngine.EventSystems;

namespace Jamkkaebi.Scripts.UI.Dev
{
    // 2026-10-29 기준 미사용: 홈·도감·친구 탭의 시제품 더미 화면이 전시관·도감·친구 화면으로 교체되면서
    // 어떤 씬·프리팹에도 붙어 있지 않다. IntegratedVerification이 탭에 이 컴포넌트가 남아 있지 않은지 검사할 때만 참조한다.
    // 시제품 흐름을 참고하기 위해 Dev/로 옮겨 보관한다.
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
