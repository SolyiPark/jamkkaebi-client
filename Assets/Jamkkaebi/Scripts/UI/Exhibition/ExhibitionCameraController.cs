using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>화면별 시점을 보관하며 카메라 적용과 방문 전 시점 복구를 단독으로 담당합니다.</summary>
    public sealed class ExhibitionCameraController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField, Range(.75f, 2)] private float _focusZoom = 1.5f;
        [SerializeField, Min(0)] private float _transitionSeconds = .15f;
        private Vector2 _center;
        private float _zoom = 1;
        private Transform _focus;
        private Vector3 _savedPosition;
        private float _savedSize;
        private Vector2 _savedCenter;
        private float _savedZoom;
        private bool _captured;
        private bool _transitioning;
        private float _transitionStarted;
        private Vector3 _transitionPosition;
        private float _transitionSize;
        public Camera SceneCamera => _camera;
        public Vector2 Center => _center;
        public float Zoom => _zoom;
        public bool HasFocus => _focus && _focus.gameObject.activeInHierarchy;
        public void Configure(Camera camera) => _camera = camera;
        public void SetView(Vector2 center, float zoom) { _center = center; _zoom = Mathf.Clamp(zoom, .75f, 2); }

        public void SetFocus(Transform target)
        {
            if (_focus == target) return;
            BeginTransition();
            _focus = target;
        }

        public void ClearFocus()
        {
            // 파괴된 Unity 객체도 C# 참조는 남으므로 복귀 전환을 시작합니다.
            if (ReferenceEquals(_focus, null)) return;
            BeginTransition();
            _focus = null;
        }

        private void BeginTransition()
        {
            if (!_camera) return;
            _transitionStarted = Time.unscaledTime;
            _transitionPosition = _camera.transform.localPosition;
            _transitionSize = _camera.orthographicSize;
            _transitioning = _transitionSeconds > 0;
        }

        public void ResetView() { ClearFocus(); SetView(Vector2.zero, 1); _transitioning = false; }

        public void CaptureView()
        {
            if (_captured || !_camera) return;
            _savedPosition = _camera.transform.localPosition; _savedSize = _camera.orthographicSize;
            _savedCenter = _center; _savedZoom = _zoom; _captured = true;
        }

        public void RestoreView()
        {
            ClearFocus(); _transitioning = false;
            if (!_captured) return;
            _captured = false; _center = _savedCenter; _zoom = _savedZoom;
            if (!_camera) return;
            _camera.transform.localPosition = _savedPosition; _camera.orthographicSize = _savedSize;
        }

        /// <summary>이동 객체 갱신 이후, 배경 레이어 갱신 이전에 화면 소유자가 호출합니다.</summary>
        public void Apply()
        {
            if (!_camera) return;
            Vector2 center = _center;
            float zoom = _zoom;
            if (HasFocus)
            {
                var parent = _camera.transform.parent;
                Vector3 local = parent ? parent.InverseTransformPoint(_focus.position) : _focus.position;
                center = local; zoom = Mathf.Clamp(_zoom * _focusZoom, .75f, 2);
            }
            else ClearFocus();
            var position = new Vector3(center.x, center.y, -10);
            float size = CalculateOrthographicSize(_camera.aspect, zoom);
            if (_transitioning)
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - _transitionStarted) / _transitionSeconds));
                position = Vector3.Lerp(_transitionPosition, position, t);
                size = Mathf.Lerp(_transitionSize, size, t);
                if (t >= 1) _transitioning = false;
            }
            _camera.transform.localPosition = position;
            _camera.orthographicSize = size;
        }

        public static float CalculateOrthographicSize(float aspect, float zoom) =>
            Mathf.Max(3.84f, 2.32f / Mathf.Max(.1f, aspect)) / Mathf.Clamp(zoom, .75f, 2);
    }
}
